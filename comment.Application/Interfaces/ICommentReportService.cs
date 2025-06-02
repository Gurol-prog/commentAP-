using Comment.Domain.Entities;
using Comment.Domain.DTOs;

namespace Comment.Application.Interfaces
{
    public interface ICommentReportService
    {
        Task<CommentReport> CreateReportAsync(CommentReport report); // Yeni şikayet oluştur

        Task<List<CommentReport>> GetUnreviewedReportsAsync(); // Admin → bekleyen şikayetleri görsün

        Task<CommentReport?> GetReportByIdAsync(string reportId); // Tekil şikayeti getir (inceleme için)

        Task<bool> ReviewReportAsync(string reportId, string adminResponse); // Admin inceledi → cevapla + işaretle

        Task<bool> DeactivateReportAsync(string reportId); // Rapor pasif hale getir (örnek: kapatıldı)

        Task<List<CommentReport>> GetReportsByCommentIdAsync(string commentId); // Bir yoruma ait tüm şikayetler
        Task<List<string>> GetReportedCommentIdsByUserAsync(string userId); // Kullanıcının şikayet ettiği yorumları getir
        Task<List<CommentReport>> GetUserReportsWithDetailsAsync(string userId); // Kullanıcının şikayet detaylarını getir
        Task<object> FilterCommentReportsAsync(FilterCommentReportsRequest request); // Şikayetleri filtrele

        Task<List<object>> GetReportsWithDetails(); // detaylı tüm şikayetler

        Task<List<object>> GetUnreviewedReportsWithDetails(); // Admin → bekleyen şikayetleri görsün detaylı
        Task<object?> GetReportByIdWithDetails(string reportId); // Tekil şikayeti getir (inceleme için)- detaylı

        Task<List<object>> GetReportsByCommentIdWithDetails(string commentId); // Bir yoruma ait tüm şikayetler - detaylı

        Task<object> FilterCommentReportsWithDetailsAsync(FilterCommentReportsRequest request); // Şikayetleri filtrele - detaylı

        Task<List<object>> GetCommentsReportedAgainstUser(string userId);// Bir kullanıcının şikayet edilen yorumlarını getir

    }
}