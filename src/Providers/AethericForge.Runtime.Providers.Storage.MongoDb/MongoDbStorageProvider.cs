using System.Security.Cryptography;
using AethericForge.Runtime.Abstractions.Interfaces.Storage;
using AethericForge.Runtime.Abstractions.Interfaces.Storage.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Storage.Providers;
using AethericForge.Runtime.Models.Storage;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AethericForge.Runtime.Providers.Storage.MongoDb;

public sealed class MongoDbStorageProvider : IStorageProvider
{
    private readonly IMongoCollection<MongoStorageDocument> _collection;

    public MongoDbStorageProvider(
        string mongoUri,
        string databaseName,
        string collectionName,
        string store,
        bool directConnection = true)
        : this(
            CreateDatabase(mongoUri, databaseName, directConnection),
            store,
            collectionName)
    {
    }

    public MongoDbStorageProvider(
        IMongoDatabase database,
        string store,
        string collectionName)
    {
        ArgumentNullException.ThrowIfNull(database);

        Store = NormalizeRequired(store, nameof(store));
        _collection = database.GetCollection<MongoStorageDocument>(
            NormalizeRequired(collectionName, nameof(collectionName)));
    }

    public string Store { get; }

    public async Task<IStorageReference> PutAsync(
        string key,
        Stream content,
        IStorageMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();

        var normalizedKey = NormalizeRequired(key, nameof(key));
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var contentBytes = buffer.ToArray();

        var document = new MongoStorageDocument
        {
            Key = normalizedKey,
            Content = contentBytes,
            ContentType = NormalizeOptional(metadata?.ContentType),
            ContentLength = contentBytes.LongLength,
            ETag = CreateETag(contentBytes),
            LastModifiedUtc = DateTime.UtcNow,
            Attributes = NormalizeAttributes(metadata?.Attributes)
        };

        await _collection.ReplaceOneAsync(
            CreateKeyFilter(normalizedKey),
            document,
            new ReplaceOptions { IsUpsert = true },
            ct);

        return new StorageReference(Store, normalizedKey);
    }

    public async Task<Stream> OpenReadAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        var document = await FindAsync(reference.Key, ct);
        if (document is null)
        {
            throw new FileNotFoundException(
                $"Storage object '{reference.Store}:{reference.Key}' was not found.",
                reference.Key);
        }

        return new MemoryStream(document.Content, writable: false);
    }

    public async Task<IStorageMetadata?> StatAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        var document = await FindAsync(reference.Key, ct);
        return document is null ? null : CreateMetadata(document);
    }

    public async Task<bool> ExistsAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        var count = await _collection.CountDocumentsAsync(
            CreateKeyFilter(reference.Key),
            new CountOptions { Limit = 1 },
            ct);

        return count > 0;
    }

    public async Task<bool> DeleteAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        var result = await _collection.DeleteOneAsync(CreateKeyFilter(reference.Key), ct);
        return result.DeletedCount > 0;
    }

    private async Task<MongoStorageDocument?> FindAsync(string key, CancellationToken ct)
    {
        return await _collection.Find(CreateKeyFilter(key)).FirstOrDefaultAsync(ct);
    }

    private void EnsureOwns(IStorageReference reference)
    {
        if (!string.Equals(Store, reference.Store, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Provider store '{Store}' cannot handle reference for store '{reference.Store}'.");
        }
    }

    private static IMongoDatabase CreateDatabase(
        string mongoUri,
        string databaseName,
        bool directConnection)
    {
        var normalizedDatabaseName = NormalizeRequired(databaseName, nameof(databaseName));
        var builder = new MongoUrlBuilder(NormalizeRequired(mongoUri, nameof(mongoUri)))
        {
            DirectConnection = directConnection
        };

        return new MongoClient(builder.ToMongoUrl()).GetDatabase(normalizedDatabaseName);
    }

    private static FilterDefinition<MongoStorageDocument> CreateKeyFilter(string key)
    {
        return Builders<MongoStorageDocument>.Filter.Eq(x => x.Key, key);
    }

    private static StorageMetadata CreateMetadata(MongoStorageDocument document)
    {
        return new StorageMetadata(
            document.ContentType,
            document.ContentLength,
            document.ETag,
            new DateTimeOffset(DateTime.SpecifyKind(document.LastModifiedUtc, DateTimeKind.Utc)),
            document.Attributes);
    }

    private static string CreateETag(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Dictionary<string, string> NormalizeAttributes(
        IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in attributes)
        {
            normalized[NormalizeRequired(key, nameof(attributes))] = value;
        }

        return normalized;
    }

    private sealed class MongoStorageDocument
    {
        [BsonId]
        public string Key { get; set; } = string.Empty;

        [BsonElement("content")]
        public byte[] Content { get; set; } = [];

        [BsonElement("contentType")]
        public string? ContentType { get; set; }

        [BsonElement("contentLength")]
        public long ContentLength { get; set; }

        [BsonElement("eTag")]
        public string ETag { get; set; } = string.Empty;

        [BsonElement("lastModifiedUtc")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime LastModifiedUtc { get; set; }

        [BsonElement("attributes")]
        public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.Ordinal);
    }
}
