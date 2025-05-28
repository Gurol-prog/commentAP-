namespace Comment.Domain.DTOs
{
    public class CommentFilterRequest
    {
        public string ContentId { get; set; } = null!; // Hangi içeriğin yorumları

        public string? UserId { get; set; } // Giriş yapan kullanıcı (şikayet filtrelemesi için)

        public string? ParentCommentId { get; set; } // null → ana yorumlar, doluysa → cevaplar

        public bool? IsDeleted { get; set; } = false;// Soft delete olanlar filtrelensin mi?

        public bool? OnlyMine { get; set; } // Yalnızca kendi yorumlarını çek (UserId şart)

        public string? Search { get; set; } // İçerikte geçen kelimeye göre filtre

        public int Page { get; set; } = 1; // Sayfa numarası

        public int PageSize { get; set; } = 10; // Her sayfadaki eleman sayısı
    }
}
