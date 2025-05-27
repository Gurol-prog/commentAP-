using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Comment.Domain.Entities
{
    public class CommentVote
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("VoterId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string VoterUserId { get; set; } = null!;

        [BsonElement("CommentId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string CommentId { get; set; } = null!;

        [BsonElement("VoteType")]
        public string VoteType { get; set; } = null!; // "like" veya "dislike"

        [BsonElement("InsertTime")]
        public DateTime InsertTime { get; set; } = DateTime.UtcNow;
    }
}
