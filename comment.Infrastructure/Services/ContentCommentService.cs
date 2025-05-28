using Comment.Application.Interfaces;
using Comment.Domain.Entities;
using Comment.Domain.DTOs;
using MongoDB.Driver;

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
            await _comments.InsertOneAsync(comment);
            return comment;
        }

        public async Task<bool> UpdateCommentAsync(string id, string newComment)
        {
            var update = Builders<ContentComment>.Update
                .Set(x => x.Comment, newComment)
                .Set(x => x.LastUpdateTime, DateTime.UtcNow);

            var result = await _comments.UpdateOneAsync(x => x.Id == id && x.DeleteTime == null, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> SoftDeleteCommentAsync(string id)
        {
            var update = Builders<ContentComment>.Update.Set(x => x.DeleteTime, DateTime.UtcNow);
            var result = await _comments.UpdateOneAsync(x => x.Id == id, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteCommentAsync(string id)
        {
            var result = await _comments.DeleteOneAsync(x => x.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<bool> DeleteAllCommentsByContentIdAsync(string contentId)
        {
            var result = await _comments.DeleteManyAsync(x => x.ContentId == contentId);
            return result.DeletedCount > 0;
        }

        public async Task<long> GetCommentCountByContentIdAsync(string contentId)
        {
            return await _comments.CountDocumentsAsync(x => x.ContentId == contentId && x.DeleteTime == null);
        }

        // ✅ Sadece bu metodu güncelleyin - daha anlamlı mesajlar
        public async Task<(bool success, string message, int likeCount, int dislikeCount)> ToggleCommentLikeAsync(
             string userId, string commentId, string likeType)
        {
            // ✅ Enum string'ini normalize et
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

    }
}