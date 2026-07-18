using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.References;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Identity.Primitives;
using AethericForge.Runtime.Models.Knowledge.Artifacts;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.Representations;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AethericForge.Runtime.Providers.Knowledge.MongoDb;

public sealed class MongoDbKnowledgeProvider : IKnowledgeProvider
{
    private readonly IMongoCollection<MongoKnowledgeArtifactDocument> _artifacts;
    private readonly IMongoCollection<MongoAuthoritativeReferenceDocument> _authoritativeReferences;
    private readonly object _indexInitializationSync = new();
    private Task? _indexInitialization;

    public MongoDbKnowledgeProvider(
        string mongoUri,
        string databaseName,
        string collectionName,
        string scheme,
        bool directConnection = true)
        : this(
            CreateDatabase(mongoUri, databaseName, directConnection),
            scheme,
            collectionName)
    {
    }

    public MongoDbKnowledgeProvider(
        IMongoDatabase database,
        string scheme,
        string collectionName)
    {
        ArgumentNullException.ThrowIfNull(database);

        Scheme = NormalizeRequired(scheme, nameof(scheme));
        var normalizedCollectionName = NormalizeRequired(collectionName, nameof(collectionName));
        _artifacts = database.GetCollection<MongoKnowledgeArtifactDocument>(normalizedCollectionName);
        _authoritativeReferences = database.GetCollection<MongoAuthoritativeReferenceDocument>(
            $"{normalizedCollectionName}.authoritativeReferences");
    }

    public string Scheme { get; }

    public async Task<IKnowledgeArtifact?> GetArtifactAsync(
        IKnowledgeReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        var document = await _artifacts
            .Find(Builders<MongoKnowledgeArtifactDocument>.Filter.Eq(x => x.Id, GetKey(reference)))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ToArtifact(document);
    }

    public async Task<IReadOnlyCollection<IKnowledgeArtifact>> FindArtifactsAsync(
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authority);
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexesAsync(cancellationToken);

        var filter = Builders<MongoKnowledgeArtifactDocument>.Filter.And(
            Builders<MongoKnowledgeArtifactDocument>.Filter.Eq(
                x => x.Authority!.Identity.Scheme,
                (int)authority.Identity.Scheme),
            Builders<MongoKnowledgeArtifactDocument>.Filter.Eq(
                x => x.Authority!.Identity.SubjectId,
                authority.Identity.SubjectId),
            Builders<MongoKnowledgeArtifactDocument>.Filter.Eq(
                x => x.Authority!.Context,
                authority.Context));

        var documents = await _artifacts
            .Find(filter)
            .SortByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return documents.Select(ToArtifact).ToArray();
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        Task initialization;
        lock (_indexInitializationSync)
        {
            initialization = _indexInitialization ??= CreateIndexesAsync();
        }

        await initialization.WaitAsync(cancellationToken);
    }

    private async Task CreateIndexesAsync()
    {
        var keys = Builders<MongoKnowledgeArtifactDocument>.IndexKeys
            .Ascending("authority.identity.scheme")
            .Ascending("authority.identity.subjectId")
            .Ascending("authority.context")
            .Descending("createdAtUtc");

        await _artifacts.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoKnowledgeArtifactDocument>(
                keys,
                new CreateIndexOptions
                {
                    Name = "authority_lookup_created_desc"
                }));
    }

    public async Task<IKnowledgeArtifact> StoreArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        IKnowledgeAuthority? authority = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(representations);
        cancellationToken.ThrowIfCancellationRequested();

        var reference = new KnowledgeReference(
            Scheme,
            "Artifact",
            Guid.NewGuid().ToString("N"),
            "1.0.0");
        var timestamp = DateTimeOffset.UtcNow;
        var representationDocuments = new List<MongoKnowledgeRepresentationDocument>();

        foreach (var representation in representations)
        {
            ArgumentNullException.ThrowIfNull(representation);
            await using var content = await representation.OpenStreamAsync(cancellationToken);
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);

            representationDocuments.Add(new MongoKnowledgeRepresentationDocument
            {
                ContentType = representation.ContentType,
                Encoding = representation.Encoding,
                Language = representation.Language,
                ContentLength = buffer.Length,
                ContentHash = representation.ContentHash,
                Content = buffer.ToArray()
            });
        }

        var document = new MongoKnowledgeArtifactDocument
        {
            Id = GetKey(reference),
            Reference = ToDocument(reference),
            Descriptor = ToDocument(descriptor),
            Lifecycle = (int)KnowledgeLifecycle.Catalogued,
            State = (int)KnowledgeState.Available,
            CreatedAtUtc = timestamp.UtcDateTime,
            UpdatedAtUtc = timestamp.UtcDateTime,
            Representations = representationDocuments,
            Lineage = lineage?.Select(ToDocument).ToList() ?? [],
            Authority = authority is null ? null : ToDocument(authority)
        };

        await _artifacts.ReplaceOneAsync(
            Builders<MongoKnowledgeArtifactDocument>.Filter.Eq(x => x.Id, document.Id),
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        return ToArtifact(document);
    }

    public async Task SetAuthoritativeReferenceAsync(
        IAuthoritativeReference reference,
        IKnowledgeReference target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        var document = new MongoAuthoritativeReferenceDocument
        {
            Id = GetKey(reference),
            Target = ToDocument(target)
        };

        await _authoritativeReferences.ReplaceOneAsync(
            Builders<MongoAuthoritativeReferenceDocument>.Filter.Eq(x => x.Id, document.Id),
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<IKnowledgeReference?> ResolveAuthoritativeReferenceAsync(
        IAuthoritativeReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        var document = await _authoritativeReferences
            .Find(Builders<MongoAuthoritativeReferenceDocument>.Filter.Eq(x => x.Id, GetKey(reference)))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ToReference(document.Target);
    }

    private static IMongoDatabase CreateDatabase(
        string mongoUri,
        string databaseName,
        bool directConnection)
    {
        var builder = new MongoUrlBuilder(NormalizeRequired(mongoUri, nameof(mongoUri)))
        {
            DirectConnection = directConnection
        };

        return new MongoClient(builder.ToMongoUrl()).GetDatabase(
            NormalizeRequired(databaseName, nameof(databaseName)));
    }

    private static IKnowledgeArtifact ToArtifact(MongoKnowledgeArtifactDocument document)
    {
        var representations = document.Representations.Select(representation =>
            new KnowledgeRepresentation(
                representation.ContentType,
                representation.ContentLength,
                _ => Task.FromResult<Stream>(new MemoryStream(representation.Content, writable: false)),
                representation.Encoding,
                representation.Language,
                representation.ContentHash));

        return new KnowledgeArtifact(
            ToReference(document.Reference),
            new KnowledgeDescriptor(
                document.Descriptor.Title,
                document.Descriptor.Abstract,
                document.Descriptor.Summary,
                document.Descriptor.Description),
            representations,
            document.Lineage.Select(ToReference),
            (KnowledgeLifecycle)document.Lifecycle,
            (KnowledgeState)document.State,
            new DateTimeOffset(DateTime.SpecifyKind(document.CreatedAtUtc, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(document.UpdatedAtUtc, DateTimeKind.Utc)),
            document.Authority is null ? null : ToAuthority(document.Authority));
    }

    private static KnowledgeAuthority ToAuthority(MongoKnowledgeAuthorityDocument document)
    {
        var identity = document.Identity;
        return new KnowledgeAuthority(
            new IdentitySubject(
                identity.SubjectId,
                (IdentityScheme)identity.Scheme,
                identity.DisplayName,
                (IdentityState)identity.State,
                identity.Claims.Select(claim => new IdentityClaim(
                    claim.Type,
                    claim.Value,
                    claim.Issuer,
                    ToDateTimeOffset(claim.IssuedAtUtc),
                    ToDateTimeOffset(claim.ExpiresAtUtc)))),
            document.Context);
    }

    private static MongoKnowledgeAuthorityDocument ToDocument(IKnowledgeAuthority authority)
    {
        var identity = authority.Identity;
        return new MongoKnowledgeAuthorityDocument
        {
            Context = authority.Context,
            Identity = new MongoIdentitySubjectDocument
            {
                SubjectId = identity.SubjectId,
                Scheme = (int)identity.Scheme,
                DisplayName = identity.DisplayName,
                State = (int)identity.State,
                Claims = identity.Claims.Select(claim => new MongoIdentityClaimDocument
                {
                    Type = claim.Type,
                    Value = claim.Value,
                    Issuer = claim.Issuer,
                    IssuedAtUtc = claim.IssuedAtUtc?.UtcDateTime,
                    ExpiresAtUtc = claim.ExpiresAtUtc?.UtcDateTime
                }).ToList()
            }
        };
    }

    private static MongoKnowledgeDescriptorDocument ToDocument(IKnowledgeDescriptor descriptor) => new()
    {
        Title = descriptor.Title,
        Abstract = descriptor.Abstract,
        Summary = descriptor.Summary,
        Description = descriptor.Description
    };

    private static MongoKnowledgeReferenceDocument ToDocument(IKnowledgeReference reference) => new()
    {
        Scheme = reference.Scheme,
        Kind = reference.Kind,
        Name = reference.Name,
        Version = reference.Version,
        Revision = reference.Revision,
        ContentHash = reference.ContentHash
    };

    private static KnowledgeReference ToReference(MongoKnowledgeReferenceDocument reference) => new(
        reference.Scheme,
        reference.Kind,
        reference.Name,
        reference.Version,
        reference.Revision,
        reference.ContentHash);

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value) =>
        value is null
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static string GetKey(IKnowledgeReference reference) =>
        $"{reference.Scheme}:{reference.Kind}/{reference.Name}@{reference.Version}.{reference.Revision}";

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }

    internal sealed class MongoKnowledgeArtifactDocument
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        [BsonElement("reference")]
        public MongoKnowledgeReferenceDocument Reference { get; set; } = new();

        [BsonElement("descriptor")]
        public MongoKnowledgeDescriptorDocument Descriptor { get; set; } = new();

        [BsonElement("lifecycle")]
        public int Lifecycle { get; set; }

        [BsonElement("state")]
        public int State { get; set; }

        [BsonElement("createdAtUtc")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAtUtc { get; set; }

        [BsonElement("updatedAtUtc")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAtUtc { get; set; }

        [BsonElement("representations")]
        public List<MongoKnowledgeRepresentationDocument> Representations { get; set; } = [];

        [BsonElement("lineage")]
        public List<MongoKnowledgeReferenceDocument> Lineage { get; set; } = [];

        [BsonElement("authority")]
        public MongoKnowledgeAuthorityDocument? Authority { get; set; }
    }

    internal sealed class MongoAuthoritativeReferenceDocument
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        [BsonElement("target")]
        public MongoKnowledgeReferenceDocument Target { get; set; } = new();
    }

    internal sealed class MongoKnowledgeReferenceDocument
    {
        [BsonElement("scheme")]
        public string Scheme { get; set; } = string.Empty;

        [BsonElement("kind")]
        public string Kind { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("version")]
        public string Version { get; set; } = string.Empty;

        [BsonElement("revision")]
        public int Revision { get; set; }

        [BsonElement("contentHash")]
        public string? ContentHash { get; set; }
    }

    internal sealed class MongoKnowledgeDescriptorDocument
    {
        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("abstract")]
        public string? Abstract { get; set; }

        [BsonElement("summary")]
        public string? Summary { get; set; }

        [BsonElement("description")]
        public string? Description { get; set; }
    }

    internal sealed class MongoKnowledgeRepresentationDocument
    {
        [BsonElement("contentType")]
        public string ContentType { get; set; } = string.Empty;

        [BsonElement("encoding")]
        public string? Encoding { get; set; }

        [BsonElement("language")]
        public string? Language { get; set; }

        [BsonElement("contentLength")]
        public long ContentLength { get; set; }

        [BsonElement("contentHash")]
        public string? ContentHash { get; set; }

        [BsonElement("content")]
        public byte[] Content { get; set; } = [];
    }

    internal sealed class MongoKnowledgeAuthorityDocument
    {
        [BsonElement("identity")]
        public MongoIdentitySubjectDocument Identity { get; set; } = new();

        [BsonElement("context")]
        public string Context { get; set; } = string.Empty;
    }

    internal sealed class MongoIdentitySubjectDocument
    {
        [BsonElement("subjectId")]
        public string SubjectId { get; set; } = string.Empty;

        [BsonElement("scheme")]
        public int Scheme { get; set; }

        [BsonElement("displayName")]
        public string? DisplayName { get; set; }

        [BsonElement("state")]
        public int State { get; set; }

        [BsonElement("claims")]
        public List<MongoIdentityClaimDocument> Claims { get; set; } = [];
    }

    internal sealed class MongoIdentityClaimDocument
    {
        [BsonElement("type")]
        public string Type { get; set; } = string.Empty;

        [BsonElement("value")]
        public string Value { get; set; } = string.Empty;

        [BsonElement("issuer")]
        public string? Issuer { get; set; }

        [BsonElement("issuedAtUtc")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? IssuedAtUtc { get; set; }

        [BsonElement("expiresAtUtc")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ExpiresAtUtc { get; set; }
    }
}
