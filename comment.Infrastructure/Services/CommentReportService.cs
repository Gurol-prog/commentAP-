using Comment.Application.Interfaces;
using Comment.Domain.Entities;
using Comment.Domain.DTOs;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Comment.Infrastructure.Services
{
    public class CommentReportService : ICommentReportService
    {
        private readonly IMongoCollection<CommentReport> _reports;

        public CommentReportService(IMongoDBService mongoService)
        {
            _reports = mongoService.GetCollection<CommentReport>("CommentReports");

            // Aynı kullanıcı aynı yorumu tekrar tekrar şikayet edemesin
            var indexOptions = new CreateIndexOptions { Unique = true };
            var indexKeys = Builders<CommentReport>.IndexKeys
                .Ascending(x => x.ReporterUserId)
                .Ascending(x => x.CommentId);

            _reports.Indexes.CreateOne(new CreateIndexModel<CommentReport>(indexKeys, indexOptions));
        }

        public async Task<CommentReport> CreateReportAsync(CommentReport report)
        {
            await _reports.InsertOneAsync(report);
            return report;
        }

        public async Task<List<CommentReport>> GetUnreviewedReportsAsync()
        {
            return await _reports.Find(r => r.IsReviewed == false).ToListAsync();
        }

        public async Task<CommentReport?> GetReportByIdAsync(string reportId)
        {
            return await _reports.Find(x => x.Id == reportId).FirstOrDefaultAsync();
        }

        public async Task<bool> ReviewReportAsync(string reportId, string adminResponse)
        {
            var update = Builders<CommentReport>.Update
                .Set(r => r.AdminResponse, adminResponse)
                .Set(r => r.IsReviewed, true)
                .Set(r => r.ReviewTime, DateTime.UtcNow);

            var result = await _reports.UpdateOneAsync(r => r.Id == reportId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeactivateReportAsync(string reportId)
        {
            var update = Builders<CommentReport>.Update
                .Set(r => r.IsActive, false)
                .Set(r => r.DeactivateTime, DateTime.UtcNow);

            var result = await _reports.UpdateOneAsync(r => r.Id == reportId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<List<CommentReport>> GetReportsByCommentIdAsync(string commentId)
        {
            return await _reports.Find(r => r.CommentId == commentId).ToListAsync();
        }

        public async Task<List<string>> GetReportedCommentIdsByUserAsync(string userId)
        {
            var reports = await _reports
                .Find(x => x.ReporterUserId == userId)
                .Project(x => x.CommentId)
                .ToListAsync();

            return reports;
        }
        public async Task<List<CommentReport>> GetUserReportsWithDetailsAsync(string userId)
        {
            var reports = await _reports
                .Find(x => x.ReporterUserId == userId)
                .SortByDescending(x => x.InsertTime)
                .ToListAsync();

            return reports;
        }
        public async Task<object> FilterCommentReportsAsync(FilterCommentReportsRequest request)
        {
            var builder = Builders<CommentReport>.Filter;
            var filters = new List<FilterDefinition<CommentReport>>();

            // ReporterUserId filtresi
            if (!string.IsNullOrEmpty(request.ReporterUserId))
                filters.Add(builder.Eq(r => r.ReporterUserId, request.ReporterUserId));

            // CommentId filtresi
            if (!string.IsNullOrEmpty(request.CommentId))
                filters.Add(builder.Eq(r => r.CommentId, request.CommentId));

            // Reason filtresi
            if (!string.IsNullOrEmpty(request.Reason))
                filters.Add(builder.Regex(r => r.Reason, new MongoDB.Bson.BsonRegularExpression(request.Reason, "i")));

            // IsReviewed filtresi
            if (request.IsReviewed.HasValue)
                filters.Add(builder.Eq(r => r.IsReviewed, request.IsReviewed.Value));

            // IsActive filtresi
            if (request.IsActive.HasValue)
                filters.Add(builder.Eq(r => r.IsActive, request.IsActive.Value));

            // AdminResponse filtresi (var mı yok mu)
            if (!string.IsNullOrEmpty(request.AdminResponse))
            {
                if (request.AdminResponse.ToLower() == "exists")
                    filters.Add(builder.Ne(r => r.AdminResponse, null));
                else if (request.AdminResponse.ToLower() == "notexists")
                    filters.Add(builder.Eq(r => r.AdminResponse, null));
                else
                    filters.Add(builder.Regex(r => r.AdminResponse, new MongoDB.Bson.BsonRegularExpression(request.AdminResponse, "i")));
            }

            // Tarih aralığı filtresi
            if (request.StartDate.HasValue)
                filters.Add(builder.Gte(r => r.InsertTime, request.StartDate.Value));

            if (request.EndDate.HasValue)
                filters.Add(builder.Lte(r => r.InsertTime, request.EndDate.Value));

            // Eğer hiç filtre yoksa, tümünü getir
            var combinedFilter = filters.Any() ? builder.And(filters) : builder.Empty;

            // Sayfalama
            int skip = (request.Page - 1) * request.PageSize;

            // Filtrelenmiş veriler
            var reports = await _reports.Find(combinedFilter)
                .SortByDescending(r => r.InsertTime)
                .Skip(skip)
                .Limit(request.PageSize)
                .ToListAsync();

            // Toplam sayı
            var totalCount = await _reports.CountDocumentsAsync(combinedFilter);

            return new
            {
                reports = reports,
                totalCount = totalCount,
                page = request.Page,
                pageSize = request.PageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
            };
        }

        public async Task<List<object>> GetReportsWithDetails()
        {
            var collection = _reports.Database.GetCollection<BsonDocument>("CommentReports");

            var pipeline = new[]
            {
       // 1. Lookup - CommentReport.CommentId → ContentComment._id
       BsonDocument.Parse(@"{
           $lookup: {
               from: 'ContentComments',
               localField: 'CommentId',
               foreignField: '_id',
               as: 'commentInfo'
           }
       }"),

       // 2. Unwind comment bilgisi
       BsonDocument.Parse(@"{
           $unwind: {
               path: '$commentInfo',
               preserveNullAndEmptyArrays: true
           }
       }"),

       // 3. Lookup - CommentReport.ReporterUserId → Parents._id (Şikayet eden)
       BsonDocument.Parse(@"{
           $lookup: {
               from: 'Parents',
               localField: 'ReporterUserId',
               foreignField: '_id',
               as: 'reporterInfo'
           }
       }"),

       // 4. Unwind reporter bilgisi
       BsonDocument.Parse(@"{
           $unwind: {
               path: '$reporterInfo',
               preserveNullAndEmptyArrays: true
           }
       }"),

       // 5. Lookup - ContentComment.CommenterId → Parents._id (Yorumu yazan)
       BsonDocument.Parse(@"{
           $lookup: {
               from: 'Parents',
               localField: 'commentInfo.CommenterId',
               foreignField: '_id',
               as: 'commenterInfo'
           }
       }"),

       // 6. Unwind commenter bilgisi
       BsonDocument.Parse(@"{
           $unwind: {
               path: '$commenterInfo',
               preserveNullAndEmptyArrays: true
           }
       }"),

       // 7. Project - İstenen formatı oluştur
       BsonDocument.Parse(@"{
           $project: {
               id: { $toString: '$_id' },
               reason: '$Reason',
               description: '$Description',
               comment: {
                   id: { $toString: '$commentInfo._id' },
                   contentId: { $toString: '$commentInfo.ContentId' },
                   content: '$commentInfo.Comment',
                   likeCount: '$commentInfo.LikeCount',
                   dislikeCount: '$commentInfo.DislikeCount',
                   insertTime: '$commentInfo.InsertTime',
                   parentCommentId: { 
                       $cond: {
                           if: { $ne: ['$commentInfo.ParentCommentId', null] },
                           then: { $toString: '$commentInfo.ParentCommentId' },
                           else: null
                       }
                   },
                   commenter: {
                       userId: { $toString: '$commenterInfo._id' },
                       fullName: { 
                           $cond: {
                               if: { $and: [{ $ne: ['$commenterInfo.Name', null] }, { $ne: ['$commenterInfo.Surname', null] }] },
                               then: { $concat: ['$commenterInfo.Name', ' ', '$commenterInfo.Surname'] },
                               else: 'Bilinmeyen Kullanıcı'
                           }
                       }
                   }
               },
               reporter: {
                   userId: { $toString: '$reporterInfo._id' },
                   fullName: { 
                       $cond: {
                           if: { $and: [{ $ne: ['$reporterInfo.Name', null] }, { $ne: ['$reporterInfo.Surname', null] }] },
                           then: { $concat: ['$reporterInfo.Name', ' ', '$reporterInfo.Surname'] },
                           else: 'Bilinmeyen Kullanıcı'
                       }
                   }
               },
               insertTime: '$InsertTime',
               isReviewed: '$IsReviewed',
               reviewTime: '$ReviewTime',
               adminResponse: '$AdminResponse',
               isActive: '$IsActive',
               deactivateTime: '$DeactivateTime'
           }
       }"),

       // 8. Sıralama - En yeni şikayetler önce
       BsonDocument.Parse(@"{ $sort: { InsertTime: -1 } }")
   };

            var bsonResults = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();

            var results = new List<object>();
            foreach (var doc in bsonResults)
            {
                results.Add(new
                {
                    id = doc.GetValue("id", "").ToString(),
                    reason = doc.GetValue("reason", "").ToString(),
                    description = doc.GetValue("description", "").ToString(),
                    comment = new
                    {
                        id = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("id", "").ToString(),
                        contentId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("contentId", "").ToString(),
                        content = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("content", "").ToString(),
                        likeCount = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("likeCount", 0).ToInt32(),
                        dislikeCount = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("dislikeCount", 0).ToInt32(),
                        insertTime = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                        parentCommentId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.Contains("parentCommentId") &&
                                        !doc.GetValue("comment", new BsonDocument()).AsBsonDocument["parentCommentId"].IsBsonNull
                            ? doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("parentCommentId", "").ToString()
                            : null,
                        commenter = new
                        {
                            userId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                            fullName = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                        }
                    },
                    reporter = new
                    {
                        userId = doc.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                        fullName = doc.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                    },
                    insertTime = doc.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                    isReviewed = doc.GetValue("isReviewed", false).ToBoolean(),
                    reviewTime = doc.Contains("reviewTime") && !doc["reviewTime"].IsBsonNull
                        ? doc.GetValue("reviewTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null,
                    adminResponse = doc.GetValue("adminResponse", "").ToString(),
                    isActive = doc.GetValue("isActive", true).ToBoolean(),
                    deactivateTime = doc.Contains("deactivateTime") && !doc["deactivateTime"].IsBsonNull
                        ? doc.GetValue("deactivateTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null
                });
            }

            return results;
        }

        // Admin paneli için incelenmemiş şikayetleri detaylı şekilde getir

        public async Task<List<object>> GetUnreviewedReportsWithDetails()
        {
            var collection = _reports.Database.GetCollection<BsonDocument>("CommentReports");

            var pipeline = new[]
            {
        // 1. Match - Sadece incelenmemiş şikayetler
        BsonDocument.Parse(@"{ $match: { IsReviewed: false } }"),

        // 2. Lookup - CommentReport.CommentId → ContentComment._id
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'ContentComments',
                localField: 'CommentId',
                foreignField: '_id',
                as: 'commentInfo'
            }
        }"),

        // 3. Unwind comment bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$commentInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 4. Lookup - CommentReport.ReporterUserId → Parents._id (Şikayet eden)
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'ReporterUserId',
                foreignField: '_id',
                as: 'reporterInfo'
            }
        }"),

        // 5. Unwind reporter bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$reporterInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 6. Lookup - ContentComment.CommenterId → Parents._id (Yorumu yazan)
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'commentInfo.CommenterId',
                foreignField: '_id',
                as: 'commenterInfo'
            }
        }"),

        // 7. Unwind commenter bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$commenterInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 8. Project - İstenen formatı oluştur
        BsonDocument.Parse(@"{
            $project: {
                id: { $toString: '$_id' },
                reason: '$Reason',
                description: '$Description',
                comment: {
                    id: { $toString: '$commentInfo._id' },
                    contentId: { $toString: '$commentInfo.ContentId' },
                    comment: '$commentInfo.Comment',
                    likeCount: '$commentInfo.LikeCount',
                    dislikeCount: '$commentInfo.DislikeCount',
                    insertTime: '$commentInfo.InsertTime',
                    parentCommentId: { 
                        $cond: {
                            if: { $ne: ['$commentInfo.ParentCommentId', null] },
                            then: { $toString: '$commentInfo.ParentCommentId' },
                            else: null
                        }
                    },
                    commenter: {
                        userId: { $toString: '$commenterInfo._id' },
                        fullName: { 
                            $cond: {
                                if: { $and: [{ $ne: ['$commenterInfo.Name', null] }, { $ne: ['$commenterInfo.Surname', null] }] },
                                then: { $concat: ['$commenterInfo.Name', ' ', '$commenterInfo.Surname'] },
                                else: 'Bilinmeyen Kullanıcı'
                            }
                        }
                    }
                },
                reporter: {
                    userId: { $toString: '$reporterInfo._id' },
                    fullName: { 
                        $cond: {
                            if: { $and: [{ $ne: ['$reporterInfo.Name', null] }, { $ne: ['$reporterInfo.Surname', null] }] },
                            then: { $concat: ['$reporterInfo.Name', ' ', '$reporterInfo.Surname'] },
                            else: 'Bilinmeyen Kullanıcı'
                        }
                    }
                },
                insertTime: '$InsertTime',
                isReviewed: '$IsReviewed',
                reviewTime: '$ReviewTime',
                adminResponse: '$AdminResponse',
                isActive: '$IsActive',
                deactivateTime: '$DeactivateTime'
            }
        }"),

        // 9. Sıralama - En yeni şikayetler önce
        BsonDocument.Parse(@"{ $sort: { InsertTime: -1 } }")
    };

            var bsonResults = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();

            var results = new List<object>();
            foreach (var doc in bsonResults)
            {
                results.Add(new
                {
                    id = doc.GetValue("id", "").ToString(),
                    reason = doc.GetValue("reason", "").ToString(),
                    description = doc.GetValue("description", "").ToString(),
                    comment = new
                    {
                        id = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("id", "").ToString(),
                        contentId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("contentId", "").ToString(),
                        comment = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("comment", "").ToString(),
                        likeCount = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("likeCount", 0).ToInt32(),
                        dislikeCount = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("dislikeCount", 0).ToInt32(),
                        insertTime = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                        parentCommentId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.Contains("parentCommentId") &&
                                        !doc.GetValue("comment", new BsonDocument()).AsBsonDocument["parentCommentId"].IsBsonNull
                            ? doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("parentCommentId", "").ToString()
                            : null,
                        commenter = new
                        {
                            userId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                            fullName = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                        }
                    },
                    reporter = new
                    {
                        userId = doc.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                        fullName = doc.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                    },
                    insertTime = doc.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                    isReviewed = doc.GetValue("isReviewed", false).ToBoolean(),
                    reviewTime = doc.Contains("reviewTime") && !doc["reviewTime"].IsBsonNull
                        ? doc.GetValue("reviewTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null,
                    adminResponse = doc.GetValue("adminResponse", "").ToString(),
                    isActive = doc.GetValue("isActive", true).ToBoolean(),
                    deactivateTime = doc.Contains("deactivateTime") && !doc["deactivateTime"].IsBsonNull
                        ? doc.GetValue("deactivateTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null
                });
            }

            return results;
        }

        public async Task<object?> GetReportByIdWithDetails(string reportId)
        {
            var collection = _reports.Database.GetCollection<BsonDocument>("CommentReports");

            var pipeline = new[]
            {
        // 1. Match - Belirli ID'li şikayet
        BsonDocument.Parse($@"{{ $match: {{ _id: ObjectId('{reportId}') }} }}"),

        // 2. Lookup - CommentReport.CommentId → ContentComment._id
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'ContentComments',
                localField: 'CommentId',
                foreignField: '_id',
                as: 'commentInfo'
            }
        }"),

        // 3. Unwind comment bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$commentInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 4. Lookup - CommentReport.ReporterUserId → Parents._id (Şikayet eden)
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'ReporterUserId',
                foreignField: '_id',
                as: 'reporterInfo'
            }
        }"),

        // 5. Unwind reporter bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$reporterInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 6. Lookup - ContentComment.CommenterId → Parents._id (Yorumu yazan)
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'commentInfo.CommenterId',
                foreignField: '_id',
                as: 'commenterInfo'
            }
        }"),

        // 7. Unwind commenter bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$commenterInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 8. Project - İstenen formatı oluştur
        BsonDocument.Parse(@"{
            $project: {
                id: { $toString: '$_id' },
                reason: '$Reason',
                description: '$Description',
                comment: {
                    id: { $toString: '$commentInfo._id' },
                    contentId: { $toString: '$commentInfo.ContentId' },
                    comment: '$commentInfo.Comment',
                    likeCount: '$commentInfo.LikeCount',
                    dislikeCount: '$commentInfo.DislikeCount',
                    insertTime: '$commentInfo.InsertTime',
                    parentCommentId: { 
                        $cond: {
                            if: { $ne: ['$commentInfo.ParentCommentId', null] },
                            then: { $toString: '$commentInfo.ParentCommentId' },
                            else: null
                        }
                    },
                    commenter: {
                        userId: { $toString: '$commenterInfo._id' },
                        fullName: { 
                            $cond: {
                                if: { $and: [{ $ne: ['$commenterInfo.Name', null] }, { $ne: ['$commenterInfo.Surname', null] }] },
                                then: { $concat: ['$commenterInfo.Name', ' ', '$commenterInfo.Surname'] },
                                else: 'Bilinmeyen Kullanıcı'
                            }
                        }
                    }
                },
                reporter: {
                    userId: { $toString: '$reporterInfo._id' },
                    fullName: { 
                        $cond: {
                            if: { $and: [{ $ne: ['$reporterInfo.Name', null] }, { $ne: ['$reporterInfo.Surname', null] }] },
                            then: { $concat: ['$reporterInfo.Name', ' ', '$reporterInfo.Surname'] },
                            else: 'Bilinmeyen Kullanıcı'
                        }
                    }
                },
                insertTime: '$InsertTime',
                isReviewed: '$IsReviewed',
                reviewTime: '$ReviewTime',
                adminResponse: '$AdminResponse',
                isActive: '$IsActive',
                deactivateTime: '$DeactivateTime'
            }
        }")
    };

            var bsonResults = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
            var doc = bsonResults.FirstOrDefault();

            if (doc == null)
                return null;

            return new
            {
                id = doc.GetValue("id", "").ToString(),
                reason = doc.GetValue("reason", "").ToString(),
                description = doc.GetValue("description", "").ToString(),
                comment = new
                {
                    id = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("id", "").ToString(),
                    contentId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("contentId", "").ToString(),
                    comment = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("comment", "").ToString(),
                    likeCount = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("likeCount", 0).ToInt32(),
                    dislikeCount = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("dislikeCount", 0).ToInt32(),
                    insertTime = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                    parentCommentId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.Contains("parentCommentId") &&
                                    !doc.GetValue("comment", new BsonDocument()).AsBsonDocument["parentCommentId"].IsBsonNull
                        ? doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("parentCommentId", "").ToString()
                        : null,
                    commenter = new
                    {
                        userId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                        fullName = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                    }
                },
                reporter = new
                {
                    userId = doc.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                    fullName = doc.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                },
                insertTime = doc.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                isReviewed = doc.GetValue("isReviewed", false).ToBoolean(),
                reviewTime = doc.Contains("reviewTime") && !doc["reviewTime"].IsBsonNull
                    ? doc.GetValue("reviewTime", DateTime.UtcNow).ToUniversalTime()
                    : (DateTime?)null,
                adminResponse = doc.GetValue("adminResponse", "").ToString(),
                isActive = doc.GetValue("isActive", true).ToBoolean(),
                deactivateTime = doc.Contains("deactivateTime") && !doc["deactivateTime"].IsBsonNull
                    ? doc.GetValue("deactivateTime", DateTime.UtcNow).ToUniversalTime()
                    : (DateTime?)null
            };
        }

        public async Task<List<object>> GetReportsByCommentIdWithDetails(string commentId)
        {
            var collection = _reports.Database.GetCollection<BsonDocument>("CommentReports");

            var pipeline = new[]
            {
        // 1. Match - Belirli CommentId'li şikayetler
        BsonDocument.Parse($@"{{ $match: {{ CommentId: ObjectId('{commentId}') }} }}"),

        // 2. Lookup - CommentReport.CommentId → ContentComment._id
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'ContentComments',
                localField: 'CommentId',
                foreignField: '_id',
                as: 'commentInfo'
            }
        }"),

        // 3. Unwind comment bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$commentInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 4. Lookup - CommentReport.ReporterUserId → Parents._id (Şikayet eden)
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'ReporterUserId',
                foreignField: '_id',
                as: 'reporterInfo'
            }
        }"),

        // 5. Unwind reporter bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$reporterInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 6. Lookup - ContentComment.CommenterId → Parents._id (Yorumu yazan)
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'commentInfo.CommenterId',
                foreignField: '_id',
                as: 'commenterInfo'
            }
        }"),

        // 7. Unwind commenter bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$commenterInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 8. Project - İstenen formatı oluştur
        BsonDocument.Parse(@"{
            $project: {
                id: { $toString: '$_id' },
                reason: '$Reason',
                description: '$Description',
                comment: {
                    id: { $toString: '$commentInfo._id' },
                    contentId: { $toString: '$commentInfo.ContentId' },
                    comment: '$commentInfo.Comment',
                    likeCount: '$commentInfo.LikeCount',
                    dislikeCount: '$commentInfo.DislikeCount',
                    insertTime: '$commentInfo.InsertTime',
                    parentCommentId: { 
                        $cond: {
                            if: { $ne: ['$commentInfo.ParentCommentId', null] },
                            then: { $toString: '$commentInfo.ParentCommentId' },
                            else: null
                        }
                    },
                    commenter: {
                        userId: { $toString: '$commenterInfo._id' },
                        fullName: { 
                            $cond: {
                                if: { $and: [{ $ne: ['$commenterInfo.Name', null] }, { $ne: ['$commenterInfo.Surname', null] }] },
                                then: { $concat: ['$commenterInfo.Name', ' ', '$commenterInfo.Surname'] },
                                else: 'Bilinmeyen Kullanıcı'
                            }
                        }
                    }
                },
                reporter: {
                    userId: { $toString: '$reporterInfo._id' },
                    fullName: { 
                        $cond: {
                            if: { $and: [{ $ne: ['$reporterInfo.Name', null] }, { $ne: ['$reporterInfo.Surname', null] }] },
                            then: { $concat: ['$reporterInfo.Name', ' ', '$reporterInfo.Surname'] },
                            else: 'Bilinmeyen Kullanıcı'
                        }
                    }
                },
                insertTime: '$InsertTime',
                isReviewed: '$IsReviewed',
                reviewTime: '$ReviewTime',
                adminResponse: '$AdminResponse',
                isActive: '$IsActive',
                deactivateTime: '$DeactivateTime'
            }
        }"),

        // 9. Sıralama - En yeni şikayetler önce
        BsonDocument.Parse(@"{ $sort: { InsertTime: -1 } }")
    };

            var bsonResults = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();

            var results = new List<object>();
            foreach (var doc in bsonResults)
            {
                results.Add(new
                {
                    id = doc.GetValue("id", "").ToString(),
                    reason = doc.GetValue("reason", "").ToString(),
                    description = doc.GetValue("description", "").ToString(),
                    comment = new
                    {
                        id = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("id", "").ToString(),
                        contentId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("contentId", "").ToString(),
                        comment = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("comment", "").ToString(),
                        likeCount = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("likeCount", 0).ToInt32(),
                        dislikeCount = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("dislikeCount", 0).ToInt32(),
                        insertTime = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                        parentCommentId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.Contains("parentCommentId") &&
                                        !doc.GetValue("comment", new BsonDocument()).AsBsonDocument["parentCommentId"].IsBsonNull
                            ? doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("parentCommentId", "").ToString()
                            : null,
                        commenter = new
                        {
                            userId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                            fullName = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                        }
                    },
                    reporter = new
                    {
                        userId = doc.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                        fullName = doc.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                    },
                    insertTime = doc.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                    isReviewed = doc.GetValue("isReviewed", false).ToBoolean(),
                    reviewTime = doc.Contains("reviewTime") && !doc["reviewTime"].IsBsonNull
                        ? doc.GetValue("reviewTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null,
                    adminResponse = doc.GetValue("adminResponse", "").ToString(),
                    isActive = doc.GetValue("isActive", true).ToBoolean(),
                    deactivateTime = doc.Contains("deactivateTime") && !doc["deactivateTime"].IsBsonNull
                        ? doc.GetValue("deactivateTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null
                });
            }

            return results;
        }

        // detaylı filtrelme kısmı başlangıç yeri
        public async Task<object> FilterCommentReportsWithDetailsAsync(FilterCommentReportsRequest request)
        {
            var collection = _reports.Database.GetCollection<BsonDocument>("CommentReports");

            // MongoDB match filtresi oluştur
            var matchFilter = new BsonDocument();

            // ReporterUserId filtresi
            if (!string.IsNullOrEmpty(request.ReporterUserId))
                matchFilter.Add("ReporterUserId", new ObjectId(request.ReporterUserId));

            // CommentId filtresi
            if (!string.IsNullOrEmpty(request.CommentId))
                matchFilter.Add("CommentId", new ObjectId(request.CommentId));

            // Reason filtresi
            if (!string.IsNullOrEmpty(request.Reason))
                matchFilter.Add("Reason", new BsonDocument("$regex", new BsonDocument { { "$regex", request.Reason }, { "$options", "i" } }));

            // IsReviewed filtresi
            if (request.IsReviewed.HasValue)
                matchFilter.Add("IsReviewed", request.IsReviewed.Value);

            // IsActive filtresi
            if (request.IsActive.HasValue)
                matchFilter.Add("IsActive", request.IsActive.Value);

            // AdminResponse filtresi
            if (!string.IsNullOrEmpty(request.AdminResponse))
            {
                if (request.AdminResponse.ToLower() == "exists")
                    matchFilter.Add("AdminResponse", new BsonDocument("$ne", BsonNull.Value));
                else if (request.AdminResponse.ToLower() == "notexists")
                    matchFilter.Add("AdminResponse", BsonNull.Value);
                else
                    matchFilter.Add("AdminResponse", new BsonDocument("$regex", new BsonDocument { { "$regex", request.AdminResponse }, { "$options", "i" } }));
            }

            // Tarih aralığı filtresi
            if (request.StartDate.HasValue || request.EndDate.HasValue)
            {
                var dateFilter = new BsonDocument();
                if (request.StartDate.HasValue)
                    dateFilter.Add("$gte", request.StartDate.Value);
                if (request.EndDate.HasValue)
                    dateFilter.Add("$lte", request.EndDate.Value);

                matchFilter.Add("InsertTime", dateFilter);
            }

            var pipeline = new List<BsonDocument>();

            // Match filtresi varsa ekle
            if (matchFilter.ElementCount > 0)
                pipeline.Add(new BsonDocument("$match", matchFilter));

            // Lookup'ları ekle
            pipeline.AddRange(new[]
            {
        // Lookup - CommentReport.CommentId → ContentComment._id
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'ContentComments',
                localField: 'CommentId',
                foreignField: '_id',
                as: 'commentInfo'
            }
        }"),

        // Unwind comment bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$commentInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // Lookup - CommentReport.ReporterUserId → Parents._id (Şikayet eden)
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'ReporterUserId',
                foreignField: '_id',
                as: 'reporterInfo'
            }
        }"),

        // Unwind reporter bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$reporterInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // Lookup - ContentComment.CommenterId → Parents._id (Yorumu yazan)
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'commentInfo.CommenterId',
                foreignField: '_id',
                as: 'commenterInfo'
            }
        }"),

        // Unwind commenter bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$commenterInfo',
                preserveNullAndEmptyArrays: true
            }
        }")
    });

            // CommenterUserId filtresi için ek match ekle (lookup'tan sonra)
            if (!string.IsNullOrEmpty(request.CommenterUserId))
            {
                pipeline.Add(BsonDocument.Parse($@"{{ $match: {{ 'commentInfo.CommenterId': ObjectId('{request.CommenterUserId}') }} }}"));
            }

            // Project ve sort ekle
            pipeline.AddRange(new[]
            {
        // Project - İstenen formatı oluştur
        BsonDocument.Parse(@"{
            $project: {
                id: { $toString: '$_id' },
                reason: '$Reason',
                description: '$Description',
                comment: {
                    id: { $toString: '$commentInfo._id' },
                    contentId: { $toString: '$commentInfo.ContentId' },
                    comment: '$commentInfo.Comment',
                    likeCount: '$commentInfo.LikeCount',
                    dislikeCount: '$commentInfo.DislikeCount',
                    insertTime: '$commentInfo.InsertTime',
                    parentCommentId: { 
                        $cond: {
                            if: { $ne: ['$commentInfo.ParentCommentId', null] },
                            then: { $toString: '$commentInfo.ParentCommentId' },
                            else: null
                        }
                    },
                    commenter: {
                        userId: { $toString: '$commenterInfo._id' },
                        fullName: { 
                            $cond: {
                                if: { $and: [{ $ne: ['$commenterInfo.Name', null] }, { $ne: ['$commenterInfo.Surname', null] }] },
                                then: { $concat: ['$commenterInfo.Name', ' ', '$commenterInfo.Surname'] },
                                else: 'Bilinmeyen Kullanıcı'
                            }
                        }
                    }
                },
                reporter: {
                    userId: { $toString: '$reporterInfo._id' },
                    fullName: { 
                        $cond: {
                            if: { $and: [{ $ne: ['$reporterInfo.Name', null] }, { $ne: ['$reporterInfo.Surname', null] }] },
                            then: { $concat: ['$reporterInfo.Name', ' ', '$reporterInfo.Surname'] },
                            else: 'Bilinmeyen Kullanıcı'
                        }
                    }
                },
                insertTime: '$InsertTime',
                isReviewed: '$IsReviewed',
                reviewTime: '$ReviewTime',
                adminResponse: '$AdminResponse',
                isActive: '$IsActive',
                deactivateTime: '$DeactivateTime'
            }
        }"),

        // Sıralama
        BsonDocument.Parse(@"{ $sort: { InsertTime: -1 } }")
    });

            // Toplam sayıyı hesapla (sayfalama öncesi)
            var countPipeline = new List<BsonDocument>(pipeline.Take(pipeline.Count - 1)); // Son sort'u çıkar
            countPipeline.Add(BsonDocument.Parse(@"{ $count: 'total' }"));
            var countResult = await collection.Aggregate<BsonDocument>(countPipeline).ToListAsync();
            var totalCount = countResult.FirstOrDefault()?.GetValue("total", 0).ToInt64() ?? 0;

            // Sayfalama ekle
            pipeline.Add(BsonDocument.Parse($@"{{ $skip: {(request.Page - 1) * request.PageSize} }}"));
            pipeline.Add(BsonDocument.Parse($@"{{ $limit: {request.PageSize} }}"));

            var bsonResults = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();

            var reports = new List<object>();
            foreach (var doc in bsonResults)
            {
                reports.Add(new
                {
                    id = doc.GetValue("id", "").ToString(),
                    reason = doc.GetValue("reason", "").ToString(),
                    description = doc.GetValue("description", "").ToString(),
                    comment = new
                    {
                        id = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("id", "").ToString(),
                        contentId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("contentId", "").ToString(),
                        comment = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("comment", "").ToString(),
                        likeCount = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("likeCount", 0).ToInt32(),
                        dislikeCount = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("dislikeCount", 0).ToInt32(),
                        insertTime = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                        parentCommentId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.Contains("parentCommentId") &&
                                        !doc.GetValue("comment", new BsonDocument()).AsBsonDocument["parentCommentId"].IsBsonNull
                            ? doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("parentCommentId", "").ToString()
                            : null,
                        commenter = new
                        {
                            userId = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                            fullName = doc.GetValue("comment", new BsonDocument()).AsBsonDocument.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                        }
                    },
                    reporter = new
                    {
                        userId = doc.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                        fullName = doc.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                    },
                    insertTime = doc.GetValue("insertTime", DateTime.UtcNow).ToUniversalTime(),
                    isReviewed = doc.GetValue("isReviewed", false).ToBoolean(),
                    reviewTime = doc.Contains("reviewTime") && !doc["reviewTime"].IsBsonNull
                        ? doc.GetValue("reviewTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null,
                    adminResponse = doc.GetValue("adminResponse", "").ToString(),
                    isActive = doc.GetValue("isActive", true).ToBoolean(),
                    deactivateTime = doc.Contains("deactivateTime") && !doc["deactivateTime"].IsBsonNull
                        ? doc.GetValue("deactivateTime", DateTime.UtcNow).ToUniversalTime()
                        : (DateTime?)null
                });
            }

            return new
            {
                reports = reports,
                totalCount = totalCount,
                page = request.Page,
                pageSize = request.PageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
            };
        }


        // Detaylı fitreleme kısmı sonu 

        public async Task<List<object>> GetCommentsReportedAgainstUser(string userId)
        {
            var collection = _reports.Database.GetCollection<BsonDocument>("CommentReports");

            var pipeline = new[]
            {
        // 1. Lookup - CommentReport.CommentId → ContentComment._id
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'ContentComments',
                localField: 'CommentId',
                foreignField: '_id',
                as: 'commentInfo'
            }
        }"),

        // 2. Unwind comment bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$commentInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 3. Match - Sadece belirli kullanıcının yazdığı yorumların şikayetleri
        BsonDocument.Parse($@"{{ $match: {{ 'commentInfo.CommenterId': ObjectId('{userId}') }} }}"),

        // 4. Lookup - CommentReport.ReporterUserId → Parents._id (Şikayet eden)
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'ReporterUserId',
                foreignField: '_id',
                as: 'reporterInfo'
            }
        }"),

        // 5. Unwind reporter bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$reporterInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 6. Lookup - ContentComment.CommenterId → Parents._id (Yorumu yazan - şikayet edilen)
        BsonDocument.Parse(@"{
            $lookup: {
                from: 'Parents',
                localField: 'commentInfo.CommenterId',
                foreignField: '_id',
                as: 'commenterInfo'
            }
        }"),

        // 7. Unwind commenter bilgisi
        BsonDocument.Parse(@"{
            $unwind: {
                path: '$commenterInfo',
                preserveNullAndEmptyArrays: true
            }
        }"),

        // 8. Group - Aynı yorum için birden fazla şikayet varsa grupla
        BsonDocument.Parse(@"{
            $group: {
                _id: '$CommentId',
                comment: { $first: '$commentInfo' },
                commenter: { $first: '$commenterInfo' },
                reports: {
                    $push: {
                        reportId: { $toString: '$_id' },
                        reason: '$Reason',
                        description: '$Description',
                        reportDate: '$InsertTime',
                        isReviewed: '$IsReviewed',
                        adminResponse: '$AdminResponse',
                        reporter: {
                            userId: { $toString: '$reporterInfo._id' },
                            fullName: { 
                                $cond: {
                                    if: { $and: [{ $ne: ['$reporterInfo.Name', null] }, { $ne: ['$reporterInfo.Surname', null] }] },
                                    then: { $concat: ['$reporterInfo.Name', ' ', '$reporterInfo.Surname'] },
                                    else: 'Bilinmeyen Kullanıcı'
                                }
                            }
                        }
                    }
                },
                totalReports: { $sum: 1 },
                latestReportDate: { $max: '$InsertTime' }
            }
        }"),

        // 9. Project - Final format
        BsonDocument.Parse(@"{
            $project: {
                commentId: { $toString: '$_id' },
                contentId: { $toString: '$comment.ContentId' },
                comment: '$comment.Comment',
                likeCount: '$comment.LikeCount',
                dislikeCount: '$comment.DislikeCount',
                commentDate: '$comment.InsertTime',
                parentCommentId: { 
                    $cond: {
                        if: { $ne: ['$comment.ParentCommentId', null] },
                        then: { $toString: '$comment.ParentCommentId' },
                        else: null
                    }
                },
                commenter: {
                    userId: { $toString: '$commenter._id' },
                    fullName: { 
                        $cond: {
                            if: { $and: [{ $ne: ['$commenter.Name', null] }, { $ne: ['$commenter.Surname', null] }] },
                            then: { $concat: ['$commenter.Name', ' ', '$commenter.Surname'] },
                            else: 'Bilinmeyen Kullanıcı'
                        }
                    }
                },
                totalReports: '$totalReports',
                latestReportDate: '$latestReportDate',
                reports: '$reports'
            }
        }"),

        // 10. Sıralama - En çok şikayet edilen önce
        BsonDocument.Parse(@"{ $sort: { totalReports: -1, latestReportDate: -1 } }")
    };

            var bsonResults = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();

            var results = new List<object>();
            foreach (var doc in bsonResults)
            {
                results.Add(new
                {
                    commentId = doc.GetValue("commentId", "").ToString(),
                    contentId = doc.GetValue("contentId", "").ToString(),
                    comment = doc.GetValue("comment", "").ToString(),
                    likeCount = doc.GetValue("likeCount", 0).ToInt32(),
                    dislikeCount = doc.GetValue("dislikeCount", 0).ToInt32(),
                    commentDate = doc.GetValue("commentDate", DateTime.UtcNow).ToUniversalTime(),
                    parentCommentId = doc.Contains("parentCommentId") && !doc["parentCommentId"].IsBsonNull
                        ? doc.GetValue("parentCommentId", "").ToString()
                        : null,
                    commenter = new
                    {
                        userId = doc.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                        fullName = doc.GetValue("commenter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                    },
                    totalReports = doc.GetValue("totalReports", 0).ToInt32(),
                    latestReportDate = doc.GetValue("latestReportDate", DateTime.UtcNow).ToUniversalTime(),
                    reports = doc.GetValue("reports", new BsonArray()).AsBsonArray.Select(r => new
                    {
                        reportId = r.AsBsonDocument.GetValue("reportId", "").ToString(),
                        reason = r.AsBsonDocument.GetValue("reason", "").ToString(),
                        description = r.AsBsonDocument.GetValue("description", "").ToString(),
                        reportDate = r.AsBsonDocument.GetValue("reportDate", DateTime.UtcNow).ToUniversalTime(),
                        isReviewed = r.AsBsonDocument.GetValue("isReviewed", false).ToBoolean(),
                        adminResponse = r.AsBsonDocument.GetValue("adminResponse", "").ToString(),
                        reporter = new
                        {
                            userId = r.AsBsonDocument.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("userId", "").ToString(),
                            fullName = r.AsBsonDocument.GetValue("reporter", new BsonDocument()).AsBsonDocument.GetValue("fullName", "Bilinmeyen").ToString()
                        }
                    }).ToList()
                });
            }

            return results;
        }


    }
}