using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Comment.Domain.Entities
{
    public class CommentReport
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!; // Şikayet kaydının MongoDB ObjectId'si

        [BsonElement("CommentId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string CommentId { get; set; } = null!; // Şikayet edilen yorumun ID’si

        [BsonElement("ReporterUserId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ReporterUserId { get; set; } = null!; // Şikayet eden kullanıcının ID’si

        [BsonElement("Reason")]
        public string Reason { get; set; } = null!; // Şikayet sebebi (örnek: "spam", "hakaret")

        [BsonElement("Description")]
        public string? Description { get; set; } // Kullanıcının ek açıklaması (isteğe bağlı)

        [BsonElement("InsertTime")]
        public DateTime InsertTime { get; set; } = DateTime.UtcNow; // Şikayetin oluşturulma tarihi

        [BsonElement("IsReviewed")]
        public bool IsReviewed { get; set; } = false; // Admin tarafından incelendi mi?

        [BsonElement("ReviewTime")]
        public DateTime? ReviewTime { get; set; } // İncelendiyse zamanı

        [BsonElement("AdminResponse")]
        public string? AdminResponse { get; set; } // Admin’in verdiği cevap (örnek: "yorum kaldırıldı")

        [BsonElement("IsActive")]
        public bool IsActive { get; set; } = true; // Şikayet hâlâ aktif mi? false ise kapatılmıştır
    }
}
