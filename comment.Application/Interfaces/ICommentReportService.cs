using Comment.Domain.Entities;

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

    }
}