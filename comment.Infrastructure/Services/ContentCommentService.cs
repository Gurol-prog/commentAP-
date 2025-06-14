using Comment.Application.Interfaces;
using Comment.Domain.Entities;
using Comment.Domain.DTOs;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Comment.Infrastructure.Services
{
    public class ContentCommentService : IContentCommentService
    {
        private readonly IMongoCollection<ContentComment> _comments;

        private readonly ICommentVoteService _voteService;
        private readonly ICommentReportService _reportService;


        public ContentCommentService(IMongoDBService mongoService, ICommentVoteService voteService, ICommentReportService reportService)
        {
            _comments = mongoService.GetCollection<ContentComment>("ContentComments");

            _voteService = voteService;
            _reportService = reportService;
        }

        public async Task<ContentComment?> GetByIdAsync(string id)
        {
            return await _comments.Find(x => x.Id == id && x.DeleteTime == null).FirstOrDefaultAsync();
        }

        public async Task<List<ContentComment>> GetCommentsByContentIdAsync(string contentId, string userId)
        {
            // Kullanıcının şikayet ettiği yorumlar
            var reportedIds = await _reportService.GetReportedCommentIdsByUserAsync(userId);

            // Şikayet edilen ana yorumların altındaki cevapları da bulalım
            var repliesToReported = await _comments
                .Find(x => reportedIds.Contains(x.ParentCommentId))
                .Project(x => x.Id)
                .ToListAsync();

            // Toplam gizlenecek yorumlar = şikayet edilenler + onların altındakiler
            var totalHiddenIds = reportedIds.Concat(repliesToReported).ToList();

            // Şikayet edilmemiş ana yorumları getir
            return await _comments.Find(x =>
                x.ContentId == contentId &&
                x.ParentCommentId == null &&
                x.DeleteTime == null &&
                !totalHiddenIds.Contains(x.Id))
                .SortBy(x => x.InsertTime)
                .ToListAsync();
        }

        public async Task<List<ContentComment>> GetRepliesByParentIdAsync(string parentCommentId, string userId)
        {
            // Kullanıcının şikayet ettiği yorumları al
            var reportedIds = await _reportService.GetReportedCommentIdsByUserAsync(userId);

            // Bu parent şikayet edildiyse, alt yorumları göstermeye gerek yok
            if (reportedIds.Contains(parentCommentId))
                return new List<ContentComment>();

            // Alt yorumlar içinde kullanıcı tarafından şikayet edilen varsa, onları da filtrele
            return await _comments.Find(x =>
                x.ParentCommentId == parentCommentId &&
                x.DeleteTime == null &&
                !reportedIds.Contains(x.Id))
                .SortBy(x => x.InsertTime)
                .ToListAsync();
        }

        public async Task<ContentComment> CreateCommentAsync(ContentComment comment)
        {
            // Eğer bu bir reply ise (ParentCommentId varsa)
            if (!string.IsNullOrEmpty(comment.ParentCommentId))
            {
                // Parent comment'in ReplyCount'unu +1 artır
                var parentUpdate = Builders<ContentComment>.Update.Inc(x => x.ReplyCount, 1);
                await _comments.UpdateOneAsync(
                    x => x.Id == comment.ParentCommentId && x.DeleteTime == null,
                    parentUpdate
                );
                
                // Reply comment'in ReplyCount'u null kalır (default)
                comment.ReplyCount = null;
            }
            else
            {
                // Ana yorum ise ReplyCount = 0
                comment.ReplyCount = 0;
            }

            await _comments.InsertOneAsync(comment);
            return comment;
        }

        public async Task<bool> UpdateCommentAsync(string id, string newComment, string userId)
        {
            var update = Builders<ContentComment>.Update
                .Set(x => x.Comment, newComment)
                .Set(x => x.LastUpdateTime, DateTime.UtcNow);

            // Sadece yorum sahibi güncelleyebilsin
            var result = await _comments.UpdateOneAsync(
                x => x.Id == id && x.DeleteTime == null && x.CommenterUserId == userId,
                update
            );
            return result.ModifiedCount > 0;
        }

        public async Task<bool> SoftDeleteCommentAsync(string id, string userId)
        {
            // Önce silinecek comment'i bulalım (ReplyCount mantığı için)
            var commentToDelete = await _comments.Find(x => x.Id == id && x.CommenterUserId == userId && x.DeleteTime == null).FirstOrDefaultAsync();

            if (commentToDelete == null)
                return false;

            // Comment'i soft delete yap
            var update = Builders<ContentComment>.Update.Set(x => x.DeleteTime, DateTime.UtcNow);
            var result = await _comments.UpdateOneAsync(
                x => x.Id == id && x.CommenterUserId == userId && x.DeleteTime == null,
                update
            );

            if (result.ModifiedCount > 0)
            {
                // Eğer silinen comment bir reply ise, parent'ın ReplyCount'unu azalt
                if (!string.IsNullOrEmpty(commentToDelete.ParentCommentId))
                {
                    var parentUpdate = Builders<ContentComment>.Update.Inc(x => x.ReplyCount, -1);
                    await _comments.UpdateOneAsync(
                        x => x.Id == commentToDelete.ParentCommentId && x.DeleteTime == null,
                        parentUpdate
                    );
                }
                // Eğer silinen comment ana yorum ise, altındaki tüm cevapları da sil
                else if (commentToDelete.ParentCommentId == null)
                {
                    var repliesUpdate = Builders<ContentComment>.Update.Set(x => x.DeleteTime, DateTime.UtcNow);
                    await _comments.UpdateManyAsync(
                        x => x.ParentCommentId == id && x.DeleteTime == null,
                        repliesUpdate
                    );
                }
            }

            return result.ModifiedCount > 0;
        }



        public async Task<bool> DeleteAllCommentsByContentIdAsync(string contentId)
        {
            var update = Builders<ContentComment>.Update.Set(x => x.DeleteTime, DateTime.UtcNow);
            var result = await _comments.UpdateManyAsync(
                x => x.ContentId == contentId && x.DeleteTime == null,
                update
            );
            return result.ModifiedCount > 0;
        }


        public async Task<long> GetCommentCountByContentIdAsync(string contentId)
        {
            return await _comments.CountDocumentsAsync(x => x.ContentId == contentId && x.DeleteTime == null);
        }

        public async Task<(bool success, string message, int likeCount, int dislikeCount)> ToggleCommentLikeAsync(
      string userId, string commentId, string likeType)
        {

            var likeTypeString = likeType.ToLowerInvariant();

            var existingVote = await _voteService.GetUserCommentVoteAsync(userId, commentId);

            if (existingVote == null)
            {
                // Vote yok, yeni vote ekle
                var success = await _voteService.AddCommentVoteAsync(userId, commentId, likeTypeString);
                if (success)
                {
                    var stats = await _voteService.GetCommentVoteStatsAsync(commentId);
                    return (true, $"{likeType.ToUpper()} eklendi", stats.likeCount, stats.dislikeCount);
                }
                return (false, "Vote eklenirken hata oluştu", 0, 0);
            }

            if (existingVote.VoteType == likeTypeString)
            {
                // Aynı vote var, kaldır
                var removed = await _voteService.RemoveCommentVoteAsync(userId, commentId);
                if (removed)
                {
                    var stats = await _voteService.GetCommentVoteStatsAsync(commentId);
                    return (true, $"{likeType.ToUpper()} kaldırıldı", stats.likeCount, stats.dislikeCount);
                }
                return (false, "Vote kaldırılırken hata oluştu", 0, 0);
            }

            // Farklı vote var, güncelle
            var updated = await _voteService.UpdateCommentVoteAsync(userId, commentId, likeTypeString);
            if (updated)
            {
                var updatedStats = await _voteService.GetCommentVoteStatsAsync(commentId);
                return (true, $"{likeType.ToUpper()} olarak değiştirildi", updatedStats.likeCount, updatedStats.dislikeCount);
            }

            return (false, "Vote güncellenirken hata oluştu", 0, 0);
        }


        public async Task<string?> GetUserCommentLikeStatusAsync(string userId, string commentId)
        {
            var vote = await _voteService.GetUserCommentVoteAsync(userId, commentId);
            return vote?.VoteType;
        }

        public async Task<List<ContentComment>> FilterCommentsAsync(CommentFilterRequest filter)
        {
            var builder = Builders<ContentComment>.Filter;
            var filters = new List<FilterDefinition<ContentComment>>();

            // ContentId zorunlu
            filters.Add(builder.Eq(x => x.ContentId, filter.ContentId));

            // ParentCommentId null ise ana yorumlar, doluysa alt yorumlar
            if (filter.ParentCommentId == null)
                filters.Add(builder.Eq(x => x.ParentCommentId, null));
            else
                filters.Add(builder.Eq(x => x.ParentCommentId, filter.ParentCommentId));

            // Silinmiş yorumlar (varsayılan: silinmemişler)
            if (filter.IsDeleted.HasValue)
            {
                if (filter.IsDeleted.Value)
                    filters.Add(builder.Ne(x => x.DeleteTime, null));
                else
                    filters.Add(builder.Eq(x => x.DeleteTime, null));
            }
            else
            {
                filters.Add(builder.Eq(x => x.DeleteTime, null));
            }

            // Sadece kendi yorumları
            if (filter.OnlyMine == true && !string.IsNullOrEmpty(filter.UserId))
                filters.Add(builder.Eq(x => x.CommenterUserId, filter.UserId));

            // Arama filtresi
            if (!string.IsNullOrWhiteSpace(filter.Search))
                filters.Add(builder.Regex(x => x.Comment, new MongoDB.Bson.BsonRegularExpression(filter.Search, "i")));

            // Kullanıcının şikayet ettiklerini hariç tut
            if (!string.IsNullOrEmpty(filter.UserId))
            {
                var reportedIds = await _reportService.GetReportedCommentIdsByUserAsync(filter.UserId);
                if (reportedIds.Any())
                    filters.Add(builder.Nin(x => x.Id, reportedIds));
            }

            var combinedFilter = builder.And(filters);

            // Sayfalama
            int skip = (filter.Page - 1) * filter.PageSize;

            var results = await _comments.Find(combinedFilter)
                                        .SortBy(x => x.InsertTime)
                                        .Skip(skip)
                                        .Limit(filter.PageSize)
                                        .ToListAsync();

            return results;
        }

        public async Task<List<object>> GetCommentsWithUser(string contentId, string userId)
        {
            // Report filtreleri - aynı mantık
            var reportedIds = await _reportService.GetReportedCommentIdsByUserAsync(userId);
            var repliesToReported = await _comments
                .Find(x => reportedIds.Contains(x.ParentCommentId))
                .Project(x => x.Id)
                .ToListAsync();
            var totalHiddenIds = reportedIds.Concat(repliesToReported).ToList();

            var collection = _comments.Database.GetCollection<BsonDocument>("ContentComments");

            // Match filtresini genişlet
            var matchFilter = new BsonDocument
   {
       { "ContentId", new ObjectId(contentId) },
       { "ParentCommentId", BsonNull.Value },  // Sadece ana yorumlar
       { "DeleteTime", BsonNull.Value }        // Silinmemişler
   };

            // Gizlenecek yorumları filtrele
            if (totalHiddenIds.Any())
            {
                var hiddenObjectIds = totalHiddenIds.Select(id => new ObjectId(id)).ToArray();
                matchFilter.Add("_id", new BsonDocument("$nin", new BsonArray(hiddenObjectIds)));
            }

            var pipeline = new[]
            {
       new BsonDocument("$match", matchFilter),

       BsonDocument.Parse(@"{
           $lookup: {
               from: 'Parents',
               localField: 'CommenterId',
               foreignField: '_id',
               as: 'userInfo'
           }
       }"),

       BsonDocument.Parse(@"{
           $unwind: {
               path: '$userInfo',
               preserveNullAndEmptyArrays: true
           }
       }"),

       BsonDocument.Parse(@"{
           $project: {
               id: { $toString: '$_id' },
               contentId: { $toString: '$ContentId' },
               commenterId: { $toString: '$CommenterId' },
               fullName: { 
                   $cond: {
                       if: { $and: [{ $ne: ['$userInfo.Name', null] }, { $ne: ['$userInfo.Surname', null] }] },
                       then: { $concat: ['$userInfo.Name', ' ', '$userInfo.Surname'] },
                       else: 'Bilinmeyen Kullanıcı'
                   }
               },
               comment: '$Comment',
               parentCommentId: { 
                   $cond: {
                       if: { $ne: ['$ParentCommentId', null] },
                       then: { $toString: '$ParentCommentId' },
                       else: null
                   }
               },
               likeCount: '$LikeCount',
               dislikeCount: '$DislikeCount',
               insertTime: '$InsertTime',
               lastUpdateTime: '$LastUpdateTime',
               deleteTime: '$DeleteTime',
               replyCount: '$ReplyCount'
           }
       }"),

       // Sıralama ekle
       BsonDocument.Parse(@"{ $sort: { InsertTime: 1 } }")
   };

            var bsonResults = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();

            var results = new List<object>();
            foreach (var doc in bsonResults)
            {
                results.Add(new
                {
                    id = doc.GetValue("id", "").ToString(),
                    contentId = doc.GetValue("contentId", "").ToString(),
                    user = new
                    {
                        commenterId = doc.GetValue("commenterId", "").ToString(),
                        fullName = MaskName(doc.GetValue("fullName", "Bilinmeyen").ToString())
                    },
                    comment = doc.GetValue("comment", "").ToString(),
                    parentCommentId = doc.Contains("parentCommentId") && !doc["parentCommentId"].IsBsonNull
                        ? doc.GetValue("parentCommentId", "").ToString()
                        : null,
                    likeCount = doc.GetValue("likeCount", 0).ToInt32(),
                    dislikeCount = doc.GetValue("dislikeCount", 0).ToInt32(),
                    insertTime = doc.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                    lastUpdateTime = doc.Contains("lastUpdateTime") && !doc["lastUpdateTime"].IsBsonNull
                        ? doc.GetValue("lastUpdateTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null,
                    deleteTime = doc.Contains("deleteTime") && !doc["deleteTime"].IsBsonNull
                        ? doc.GetValue("deleteTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null,
                    replyCount = doc.Contains("replyCount") && !doc["replyCount"].IsBsonNull
                        ? doc.GetValue("replyCount", 0).ToInt32()
                        : (int?)null
    
                });
            }

            return results;
        }

        public async Task<List<object>> GetRepliesWithUser(string parentCommentId, string userId)
        {
            // Report filtreleri - aynı mantık
            var reportedIds = await _reportService.GetReportedCommentIdsByUserAsync(userId);

            // Bu parent şikayet edildiyse, alt yorumları göstermeye gerek yok
            if (reportedIds.Contains(parentCommentId))
                return new List<object>();

            var collection = _comments.Database.GetCollection<BsonDocument>("ContentComments");

            // Match filtresini alt yorumlar için ayarla
            var matchFilter = new BsonDocument
    {
        { "ParentCommentId", new ObjectId(parentCommentId) },  // Bu parent'ın altındakiler
        { "DeleteTime", BsonNull.Value }                       // Silinmemişler
    };

            // Şikayet edilen yorumları filtrele
            if (reportedIds.Any())
            {
                var reportedObjectIds = reportedIds.Select(id => new ObjectId(id)).ToArray();
                matchFilter.Add("_id", new BsonDocument("$nin", new BsonArray(reportedObjectIds)));
            }

            var pipeline = new[]
            {
        new BsonDocument("$match", matchFilter),

        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'CommenterId',
                foreignField: '_id',
                as: 'userInfo'
            }
        }"),

        BsonDocument.Parse(@"{
            $unwind: {
                path: '$userInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        BsonDocument.Parse(@"{
            $project: {
                id: { $toString: '$_id' },
                contentId: { $toString: '$ContentId' },
                commenterId: { $toString: '$CommenterId' },
                fullName: { 
                    $cond: {
                        if: { $and: [{ $ne: ['$userInfo.Name', null] }, { $ne: ['$userInfo.Surname', null] }] },
                        then: { $concat: ['$userInfo.Name', ' ', '$userInfo.Surname'] },
                        else: 'Bilinmeyen Kullanıcı'
                    }
                },
                comment: '$Comment',
                parentCommentId: { $toString: '$ParentCommentId' },
                likeCount: '$LikeCount',
                dislikeCount: '$DislikeCount',
                insertTime: '$InsertTime',
                lastUpdateTime: '$LastUpdateTime',
                deleteTime: '$DeleteTime',
                replyCount: '$ReplyCount'
            }
        }"),

        // Sıralama ekle
        BsonDocument.Parse(@"{ $sort: { InsertTime: 1 } }")
    };

            var bsonResults = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();

            var results = new List<object>();
            foreach (var doc in bsonResults)
            {
                results.Add(new
                {
                    id = doc.GetValue("id", "").ToString(),
                    contentId = doc.GetValue("contentId", "").ToString(),
                    user = new
                    {
                        commenterId = doc.GetValue("commenterId", "").ToString(),
                        fullName = MaskName(doc.GetValue("fullName", "Bilinmeyen").ToString())
                    },
                    comment = doc.GetValue("comment", "").ToString(),
                    parentCommentId = doc.GetValue("parentCommentId", "").ToString(),
                    likeCount = doc.GetValue("likeCount", 0).ToInt32(),
                    dislikeCount = doc.GetValue("dislikeCount", 0).ToInt32(),
                    insertTime = doc.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                    lastUpdateTime = doc.Contains("lastUpdateTime") && !doc["lastUpdateTime"].IsBsonNull
                        ? doc.GetValue("lastUpdateTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null,
                    deleteTime = doc.Contains("deleteTime") && !doc["deleteTime"].IsBsonNull
                        ? doc.GetValue("deleteTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null,
                    replyCount = doc.Contains("replyCount") && !doc["replyCount"].IsBsonNull
                        ? doc.GetValue("replyCount", 0).ToInt32()
                        : (int?)null
                });
            }

            return results;
        }

        public async Task<List<object>> FilterCommentsWithUser(CommentFilterRequest filter)
        {
            var collection = _comments.Database.GetCollection<BsonDocument>("ContentComments");

            // Match filtresi oluştur
            var matchFilter = new BsonDocument();

            // ContentId zorunlu
            matchFilter.Add("ContentId", new ObjectId(filter.ContentId));

            // ParentCommentId filtresi
            if (filter.ParentCommentId == null)
                matchFilter.Add("ParentCommentId", BsonNull.Value);
            else
                matchFilter.Add("ParentCommentId", new ObjectId(filter.ParentCommentId));

            // Silinmiş yorumlar filtresi
            if (filter.IsDeleted.HasValue)
            {
                if (filter.IsDeleted.Value)
                    matchFilter.Add("DeleteTime", new BsonDocument("$ne", BsonNull.Value));
                else
                    matchFilter.Add("DeleteTime", BsonNull.Value);
            }
            else
            {
                matchFilter.Add("DeleteTime", BsonNull.Value);
            }

            // Sadece kendi yorumları
            if (filter.OnlyMine == true && !string.IsNullOrEmpty(filter.UserId))
                matchFilter.Add("CommenterId", new ObjectId(filter.UserId));

            // Arama filtresi
            if (!string.IsNullOrWhiteSpace(filter.Search))
                matchFilter.Add("Comment", new BsonDocument("$regex", new BsonDocument { { "$regex", filter.Search }, { "$options", "i" } }));

            // Report filtreleri
            if (!string.IsNullOrEmpty(filter.UserId))
            {
                var reportedIds = await _reportService.GetReportedCommentIdsByUserAsync(filter.UserId);
                if (reportedIds.Any())
                {
                    var reportedObjectIds = reportedIds.Select(id => new ObjectId(id)).ToArray();
                    matchFilter.Add("_id", new BsonDocument("$nin", new BsonArray(reportedObjectIds)));
                }
            }

            // Pipeline oluştur
            var pipeline = new List<BsonDocument>
    {
        new BsonDocument("$match", matchFilter),

        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'CommenterId',
                foreignField: '_id',
                as: 'userInfo'
            }
        }"),

        BsonDocument.Parse(@"{
            $unwind: {
                path: '$userInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        BsonDocument.Parse(@"{
            $project: {
                id: { $toString: '$_id' },
                contentId: { $toString: '$ContentId' },
                commenterId: { $toString: '$CommenterId' },
                fullName: { 
                    $cond: {
                        if: { $and: [{ $ne: ['$userInfo.Name', null] }, { $ne: ['$userInfo.Surname', null] }] },
                        then: { $concat: ['$userInfo.Name', ' ', '$userInfo.Surname'] },
                        else: 'Bilinmeyen Kullanıcı'
                    }
                },
                comment: '$Comment',
                parentCommentId: { 
                    $cond: {
                        if: { $ne: ['$ParentCommentId', null] },
                        then: { $toString: '$ParentCommentId' },
                        else: null
                    }
                },
                likeCount: '$LikeCount',
                dislikeCount: '$DislikeCount',
                insertTime: '$InsertTime',
                lastUpdateTime: '$LastUpdateTime',
                deleteTime: '$DeleteTime',
                replyCount: '$ReplyCount'
            }
        }"),

        // Sıralama
        BsonDocument.Parse(@"{ $sort: { InsertTime: 1 } }"),

        // Sayfalama
        BsonDocument.Parse($@"{{ $skip: {(filter.Page - 1) * filter.PageSize} }}"),
        BsonDocument.Parse($@"{{ $limit: {filter.PageSize} }}")
    };

            var bsonResults = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();

            var results = new List<object>();
            foreach (var doc in bsonResults)
            {
                results.Add(new
                {
                    id = doc.GetValue("id", "").ToString(),
                    contentId = doc.GetValue("contentId", "").ToString(),
                    user = new
                    {
                        commenterId = doc.GetValue("commenterId", "").ToString(),
                        fullName = MaskName(doc.GetValue("fullName", "Bilinmeyen").ToString())
                    },
                    comment = doc.GetValue("comment", "").ToString(),
                    parentCommentId = doc.Contains("parentCommentId") && !doc["parentCommentId"].IsBsonNull
                        ? doc.GetValue("parentCommentId", "").ToString()
                        : null,
                    likeCount = doc.GetValue("likeCount", 0).ToInt32(),
                    dislikeCount = doc.GetValue("dislikeCount", 0).ToInt32(),
                    insertTime = doc.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                    lastUpdateTime = doc.Contains("lastUpdateTime") && !doc["lastUpdateTime"].IsBsonNull
                        ? doc.GetValue("lastUpdateTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null,
                    deleteTime = doc.Contains("deleteTime") && !doc["deleteTime"].IsBsonNull
                        ? doc.GetValue("deleteTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null,
                    replyCount = doc.Contains("replyCount") && !doc["replyCount"].IsBsonNull
                        ? doc.GetValue("replyCount", 0).ToInt32()
                        : (int?)null
    
                });
            }

            return results;
        }



        private string MaskName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "Bilinmeyen";

            var parts = fullName.Split(' ');
            if (parts.Length == 0)
                return "Bilinmeyen";

            var maskedParts = new List<string>();

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                if (part.Length == 1)
                {
                    maskedParts.Add(part);
                }
                else
                {
                    maskedParts.Add(part[0] + new string('*', part.Length - 1));
                }
            }

            return maskedParts.Count > 0 ? string.Join(" ", maskedParts) : "Bilinmeyen";
        }



    }
}