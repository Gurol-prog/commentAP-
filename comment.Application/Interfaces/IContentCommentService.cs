using Comment.Domain.Entities;
using Comment.Domain.DTOs;

namespace Comment.Application.Interfaces
{
    public interface IContentCommentService
    {
        Task<ContentComment?> GetByIdAsync(string id); // ID ile yorum getir

        Task<List<ContentComment>> GetCommentsByContentIdAsync(string contentId, string userId); // Ana yorumlar (parent null)(şikayet edilenler hariç)

        Task<List<ContentComment>> GetRepliesByParentIdAsync(string parentCommentId, string userId); // Alt yorumlar (şikayet edilenler hariç)

        Task<ContentComment> CreateCommentAsync(ContentComment comment); // Yeni yorum ekle

        Task<bool> UpdateCommentAsync(string id, string newComment, string userId); // Yorumu düzenle

        Task<bool> SoftDeleteCommentAsync(string id, string userId);// Soft delete (gizle)

        Task<bool> DeleteAllCommentsByContentIdAsync(string contentId); // İçerik silinince

        Task<long> GetCommentCountByContentIdAsync(string contentId); // Yorum sayısı

        // ✅ Düzeltilmiş - parametre olarak service geçmeye gerek yok
        Task<(bool success, string message, int likeCount, int dislikeCount)> ToggleCommentLikeAsync(
            string userId,
            string commentId,
            string likeType
        );

        // ✅ Düzeltilmiş - parametre olarak service geçmeye gerek yok
        Task<string?> GetUserCommentLikeStatusAsync(
            string userId,
            string commentId
        );

        Task<List<ContentComment>> FilterCommentsAsync(CommentFilterRequest filterRequest);

        

        
    }
}