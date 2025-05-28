using Comment.Application.Interfaces;
using Comment.Domain.Entities;
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
    }
}