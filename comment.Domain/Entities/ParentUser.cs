using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Comment.Domain.Entities
{
    public class Parents
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;
        [BsonElement("Name")]
        public string Name { get; set; } = null!;
        [BsonElement("Surname")]
        public string Surname { get; set; } = null!;
        
    }
}