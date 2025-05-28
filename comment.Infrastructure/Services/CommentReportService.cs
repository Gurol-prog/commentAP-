using Comment.Application.Interfaces;
using Comment.Domain.Entities;
using Comment.Domain.DTOs;
using MongoDB.Driver;

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
    }
}