using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Comment.Domain.Entities
{
    public class ContentComment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("ContentId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ContentId { get; set; } = null!;

        [BsonElement("CommenterId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string CommenterUserId { get; set; } = null!;

        [BsonElement("Comment")]
        public string Comment { get; set; } = null!;

        [BsonElement("ParentCommentId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ParentCommentId { get; set; }

        [BsonElement("LikeCount")]
        public int LikeCount { get; set; } = 0;

        [BsonElement("DislikeCount")]
        public int DislikeCount { get; set; } = 0;

        [BsonElement("InsertTime")]
        public DateTime InsertTime { get; set; } = DateTime.UtcNow;

        [BsonElement("LastUpdateTime")]
        public DateTime? LastUpdateTime { get; set; }

        [BsonElement("DeleteTime")]
        public DateTime? DeleteTime { get; set; }
        [BsonElement("ReplyCount")]
        public int? ReplyCount { get; set; }

    }
}
