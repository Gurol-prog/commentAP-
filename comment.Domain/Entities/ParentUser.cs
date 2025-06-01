using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Comment.Domain.Entities
{
    public class Parents
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;
        [BsonElement("name")]
        public string Name { get; set; } = null!;
        [BsonElement("surName")]
        public string SurName { get; set; } = null!;
        
    }
}