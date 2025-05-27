using Comment.Application.Interfaces;
using Comment.Domain.Entities;
using MongoDB.Driver;

namespace Comment.Infrastructure.Services
{
    public class CommentVoteService : ICommentVoteService
    {
        private readonly IMongoCollection<CommentVote> _votes;
        private readonly IMongoDatabase _database;

        public CommentVoteService(IMongoDBService mongoService)
        {
            _votes = mongoService.GetCollection<CommentVote>("CommentVotes");
            _database = mongoService.GetDatabase();

            var indexOptions = new CreateIndexOptions { Unique = true };
            var indexKeys = Builders<CommentVote>.IndexKeys
                .Ascending(x => x.VoterUserId)
                .Ascending(x => x.CommentId);

            _votes.Indexes.CreateOne(new CreateIndexModel<CommentVote>(indexKeys, indexOptions));
        }

        public async Task<CommentVote?> GetUserCommentVoteAsync(string userId, string commentId)
        {
            return await _votes.Find(x => x.VoterUserId == userId && x.CommentId == commentId).FirstOrDefaultAsync();
        }

        public async Task<bool> AddCommentVoteAsync(string userId, string commentId, string voteType)
        {
            try
            {
                var vote = new CommentVote
                {
                    VoterUserId = userId,
                    CommentId = commentId,
                    VoteType = voteType.ToLowerInvariant(),
                    InsertTime = DateTime.UtcNow
                };

                await _votes.InsertOneAsync(vote);
                
                // ✅ Comment sayaçlarını güncelle
                await UpdateCommentCountersAsync(commentId);
                
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task<bool> UpdateCommentVoteAsync(string userId, string commentId, string newVoteType)
        {
            // Önce mevcut vote'u al
            var existingVote = await GetUserCommentVoteAsync(userId, commentId);
            if (existingVote == null) return false;
            
            var oldVoteType = existingVote.VoteType;
            var newVoteTypeLower = newVoteType.ToLowerInvariant();
            
            var update = Builders<CommentVote>.Update.Set(x => x.VoteType, newVoteTypeLower);
            var result = await _votes.UpdateOneAsync(
                x => x.VoterUserId == userId && x.CommentId == commentId,
                update
            );

            if (result.ModifiedCount > 0)
            {
                // ✅ Comment sayaçlarını güncelle
                await UpdateCommentCountersAsync(commentId);
            }

            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveCommentVoteAsync(string userId, string commentId)
        {
            // Önce vote'u al ki hangi tip olduğunu bilelim
            var existingVote = await GetUserCommentVoteAsync(userId, commentId);
            if (existingVote == null) return false;
            
            var result = await _votes.DeleteOneAsync(x => x.VoterUserId == userId && x.CommentId == commentId);
            
            if (result.DeletedCount > 0)
            {
                // ✅ Comment sayaçlarını güncelle
                await UpdateCommentCountersAsync(commentId);
            }
            
            return result.DeletedCount > 0;
        }

        public async Task<bool> RemoveAllVotesForCommentAsync(string commentId)
        {
            var result = await _votes.DeleteManyAsync(x => x.CommentId == commentId);
            
            if (result.DeletedCount > 0)
            {
                // ✅ Comment sayaçlarını güncelle (0 yap)
                await UpdateCommentCountersAsync(commentId);
            }
            
            return result.DeletedCount > 0;
        }

        public async Task<(int likeCount, int dislikeCount)> GetCommentVoteStatsAsync(string commentId)
        {
            var likeCount = await _votes.CountDocumentsAsync(x => x.CommentId == commentId && x.VoteType == "like");
            var dislikeCount = await _votes.CountDocumentsAsync(x => x.CommentId == commentId && x.VoteType == "dislike");

            return ((int)likeCount, (int)dislikeCount);
        }

        // ✅ Yeni metod: ContentComment tablosundaki sayaçları güncelle
        private async Task UpdateCommentCountersAsync(string commentId)
        {
            var stats = await GetCommentVoteStatsAsync(commentId);
            
            var commentCollection = _database.GetCollection<ContentComment>("ContentComments");
            var update = Builders<ContentComment>.Update
                .Set(x => x.LikeCount, stats.likeCount)
                .Set(x => x.DislikeCount, stats.dislikeCount);
            
            await commentCollection.UpdateOneAsync(
                x => x.Id == commentId,
                update
            );
        }
    }
}