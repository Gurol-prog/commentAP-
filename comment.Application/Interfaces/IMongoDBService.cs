using MongoDB.Driver;

namespace Comment.Application.Interfaces
{
    public interface IMongoDBService
    {
        IMongoCollection<T> GetCollection<T>(string collectionName);
        IMongoDatabase GetDatabase();
    }
}
