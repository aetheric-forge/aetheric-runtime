using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Models.Archive.Primitives;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

[assembly: InternalsVisibleTo("AethericForge.Runtime.Tests")]

namespace AethericForge.Runtime.Providers.Archive.MongoDb;

public sealed class MongoDbArchiveProvider : IArchiveProvider
{
    private readonly IMongoCollection<MongoArchiveDocument> _collection;

    public MongoDbArchiveProvider(
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

    public MongoDbArchiveProvider(
        IMongoDatabase database,
        string store,
        string collectionName)
    {
        ArgumentNullException.ThrowIfNull(database);

        Store = NormalizeRequired(store, nameof(store));
        _collection = database.GetCollection<MongoArchiveDocument>(
            NormalizeRequired(collectionName, nameof(collectionName)));
    }

    public string Store { get; }

    public async Task<IArchiveReference> PutAsync(
        string key,
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();

        var normalizedKey = NormalizeRequired(key, nameof(key));
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var contentBytes = buffer.ToArray();

        var document = new MongoArchiveDocument
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

        return new ArchiveReference(Store, normalizedKey);
    }

    public async Task<Stream> RetrieveAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        var document = await FindAsync(reference.Key, ct);
        if (document is null)
        {
            throw new FileNotFoundException(
                $"Archive object '{reference.Store}:{reference.Key}' was not found.",
                reference.Key);
        }

        return new MemoryStream(document.Content, writable: false);
    }

    public async Task<IArchiveMetadata?> StatAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        var document = await FindAsync(reference.Key, ct);
        return document is null ? null : CreateMetadata(document);
    }

    public async Task<bool> ExistsAsync(
        IArchiveReference reference,
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
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        var result = await _collection.DeleteOneAsync(CreateKeyFilter(reference.Key), ct);
        return result.DeletedCount > 0;
    }

    private async Task<MongoArchiveDocument?> FindAsync(string key, CancellationToken ct)
    {
        return await _collection.Find(CreateKeyFilter(key)).FirstOrDefaultAsync(ct);
    }

    private void EnsureOwns(IArchiveReference reference)
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

    private static FilterDefinition<MongoArchiveDocument> CreateKeyFilter(string key)
    {
        return Builders<MongoArchiveDocument>.Filter.Eq(x => x.Key, key);
    }

    private static ArchiveMetadata CreateMetadata(MongoArchiveDocument document)
    {
        return new ArchiveMetadata(
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

    internal sealed class MongoArchiveDocument
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
