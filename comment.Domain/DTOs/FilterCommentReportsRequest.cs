namespace Comment.Domain.DTOs
{
    public class FilterCommentReportsRequest
    {
        public string? ReporterUserId { get; set; }     // Şikayet eden kullanıcı
        public string? CommentId { get; set; }          // Şikayet edilen yorum
        public string? Reason { get; set; }             // Şikayet sebebi
        public bool? IsReviewed { get; set; }           // İncelendi mi?
        public bool? IsActive { get; set; }             // Aktif mi?
        public DateTime? StartDate { get; set; }        // Başlangıç tarihi
        public DateTime? EndDate { get; set; }          // Bitiş tarihi
        public string? AdminResponse { get; set; }      // Admin cevabı var mı?
        public int Page { get; set; } = 1;              // Sayfa numarası
        public int PageSize { get; set; } = 10;         // Sayfa boyutu
        public string? CommenterUserId { get; set; } // Yorumu yazan kullanıcı ID'si
    }
}