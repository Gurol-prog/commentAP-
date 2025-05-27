using Comment.Domain.Entities;

namespace Comment.Application.Interfaces
{
    public interface ICommentVoteService
    {
        Task<CommentVote?> GetUserCommentVoteAsync(string userId, string commentId); // Kullanıcının oyu var mı

        Task<bool> AddCommentVoteAsync(string userId, string commentId, string voteType); // İlk oy ekle

        Task<bool> UpdateCommentVoteAsync(string userId, string commentId, string newVoteType); // Oy güncelle

        Task<bool> RemoveCommentVoteAsync(string userId, string commentId); // Oy geri al

        Task<bool> RemoveAllVotesForCommentAsync(string commentId); // Yorum silinince oyları temizle

        Task<(int likeCount, int dislikeCount)> GetCommentVoteStatsAsync(string commentId); // Oy istatistikleri
    }
}