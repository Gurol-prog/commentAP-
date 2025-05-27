using Microsoft.Extensions.Configuration;
using Comment.Application.Interfaces;
using MongoDB.Driver;

namespace Comment.Infrastructure.Services
{
    public class MongoDBService : IMongoDBService
    {
        private readonly IMongoDatabase _database;

        public MongoDBService(IConfiguration configuration)
        {
            // ✅ Düzeltildi: appsettings.json'daki yapıya uygun okuma
            var connectionString = configuration["MongoDBSettings:ConnectionString"];
            var databaseName = configuration["MongoDBSettings:DatabaseName"];

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("MongoDB connection string bulunamadı! appsettings.json dosyasını kontrol edin.");
            }

            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ArgumentException("MongoDB database name bulunamadı! appsettings.json dosyasını kontrol edin.");
            }

            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<T> GetCollection<T>(string collectionName)
        {
            return _database.GetCollection<T>(collectionName);
        }
        public IMongoDatabase GetDatabase()
        {
            return _database;
        }
    }
}