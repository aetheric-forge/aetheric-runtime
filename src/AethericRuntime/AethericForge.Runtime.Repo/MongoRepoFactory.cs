using AethericForge.Runtime.Repo.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace AethericForge.Runtime.Repo;

public static class MongoRepoFactory
{
    private static bool _bsonConfigured = false;
    private static readonly object _lock = new();

    public static MongoRepo<T> Create<T>(string mongoUri, string database, string collection, bool directConnection = true) where T : IEntity
    {
        EnsureBsonSerializationConfigured();
        return new MongoRepo<T>(mongoUri, database, collection, directConnection);
    }

    private static void EnsureBsonSerializationConfigured()
    {
        if (!_bsonConfigured)
        {
            lock (_lock)
            {
                if (!_bsonConfigured)
                {
                    try
                    {
                        BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));
                    }
                    catch (MongoDB.Bson.BsonSerializationException ex)
                    {
                        // Registration may fail if already registered; ignore if it's the same config.
                        if (!ex.Message.Contains("A serializer is already registered"))
                            throw;
                    }
                    _bsonConfigured = true;
                }
            }
        }
    }
}
