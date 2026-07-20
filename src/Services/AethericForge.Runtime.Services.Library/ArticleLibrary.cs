using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Models.Identity.Primitives;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.References;
using AethericForge.Runtime.Models.Knowledge.Representations;
using AethericForge.Runtime.Models.Library.Articles;

namespace AethericForge.Runtime.Services.Library;

public sealed class ArticleLibrary : IArticleLibrary
{
    public const string ArticleContentType = "application/vnd.aethericforge.article+json";

    private const string ArticleKind = "Article";
    private const string CanonicalRole = "canonical";
    private const string SchemaVersion = "1.0.0";

    private readonly IKnowledgeService _knowledge;
    private readonly ArticleLibraryOptions _options;
    private readonly IKnowledgeAuthority _authority;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public ArticleLibrary(
        IKnowledgeService knowledge,
        ArticleLibraryOptions options)
    {
        _knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
        _options = ValidateOptions(options);
        _authority = new KnowledgeAuthority(
            new IdentitySubject(
                _options.Authority.Id,
                IdentityScheme.Service,
                _options.Authority.Name),
            _options.AuthorityContext);
    }

    public async Task<Article?> GetAsync(
        Guid canonicalId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanonicalId(canonicalId);

        var artifact = await _knowledge.ResolveReferenceAsync(
            CreateCanonicalReference(canonicalId),
            cancellationToken);

        return artifact is null
            ? null
            : await ReadArticleAsync(artifact, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Article>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var artifacts = await _knowledge.FindArtifactsAsync(
            _authority,
            cancellationToken);
        var articles = new List<Article>();

        foreach (var artifact in artifacts.Where(IsArticleArtifact))
        {
            articles.Add(await ReadArticleAsync(artifact, cancellationToken));
        }

        return articles
            .GroupBy(article => article.Identity.CanonicalId)
            .Select(revisions => revisions.MaxBy(article => article.Publication.Revision)!)
            .OrderByDescending(article => article.Provenance.UpdatedAtUtc)
            .ToArray();
    }

    public async Task<Article> CreateAsync(
        Article article,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);
        ValidateArticle(article);

        if (article.Publication.State != ArticlePublicationState.Draft)
        {
            throw new InvalidOperationException("A new article must begin as a draft.");
        }

        if (await GetAsync(article.Identity.CanonicalId, cancellationToken) is not null)
        {
            throw new InvalidOperationException(
                $"Article '{article.Identity.CanonicalId}' already exists.");
        }

        var canonical = article with
        {
            Publication = article.Publication with
            {
                Revision = 1,
                PublishedAtUtc = null,
            },
            Provenance = article.Provenance with
            {
                Authority = _options.Authority,
                Origin = article.Provenance.Origin ?? _options.DefaultOrigin,
            },
        };

        return await PublishRevisionAsync(canonical, null, cancellationToken);
    }

    public async Task<Article> SaveAsync(
        Article article,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);
        ValidateArticle(article);

        var currentArtifact = await GetArtifactAsync(
            article.Identity.CanonicalId,
            cancellationToken);
        var current = currentArtifact is null
            ? throw new KeyNotFoundException(
                $"Article '{article.Identity.CanonicalId}' was not found.")
            : await ReadArticleAsync(currentArtifact, cancellationToken);

        EnsureAuthority(article);

        if (article.Publication.State != current.Publication.State)
        {
            throw new InvalidOperationException(
                "Publication state can only be changed through TransitionAsync.");
        }

        if (article.Publication.Revision != current.Publication.Revision)
        {
            throw new InvalidOperationException(
                $"Article revision {article.Publication.Revision} is stale; " +
                $"the current revision is {current.Publication.Revision}.");
        }

        var updated = article with
        {
            Publication = article.Publication with
            {
                Revision = current.Publication.Revision + 1,
            },
            Provenance = article.Provenance with
            {
                Authority = _options.Authority,
                CreatedBy = current.Provenance.CreatedBy,
                CreatedAtUtc = current.Provenance.CreatedAtUtc,
            },
        };

        return await PublishRevisionAsync(updated, currentArtifact.Reference, cancellationToken);
    }

    public async Task<Article> TransitionAsync(
        Guid canonicalId,
        ArticlePublicationState targetState,
        ArticleAgent actor,
        DateTimeOffset? occurredAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCanonicalId(canonicalId);
        ArgumentNullException.ThrowIfNull(actor);

        var currentArtifact = await GetArtifactAsync(canonicalId, cancellationToken);
        var current = currentArtifact is null
            ? throw new KeyNotFoundException($"Article '{canonicalId}' was not found.")
            : await ReadArticleAsync(currentArtifact, cancellationToken);

        ArticleWorkflow.EnsureCanTransition(current.Publication.State, targetState);

        var occurredAt = occurredAtUtc ?? DateTimeOffset.UtcNow;
        if (targetState == ArticlePublicationState.Scheduled &&
            current.Publication.ScheduledForUtc is null)
        {
            throw new InvalidOperationException(
                "An article must have a scheduled publication date before it can be scheduled.");
        }

        var transitioned = current with
        {
            Publication = current.Publication with
            {
                State = targetState,
                Revision = current.Publication.Revision + 1,
                PublishedAtUtc = targetState == ArticlePublicationState.Published
                    ? occurredAt
                    : current.Publication.PublishedAtUtc,
            },
            Provenance = current.Provenance with
            {
                UpdatedBy = actor,
                UpdatedAtUtc = occurredAt,
                SourceHistory =
                [
                    ..current.Provenance.SourceHistory,
                    new ArticleSourceEvent
                    {
                        Source = _options.Authority.Id,
                        Action = $"Publication state changed from {current.Publication.State} to {targetState}",
                        Actor = actor,
                        OccurredAtUtc = occurredAt,
                    },
                ],
            },
        };

        return await PublishRevisionAsync(
            transitioned,
            currentArtifact.Reference,
            cancellationToken);
    }

    private async Task<Article> PublishRevisionAsync(
        Article article,
        IKnowledgeReference? previousRevision,
        CancellationToken cancellationToken)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(article, _serializerOptions);
        var artifact = await _knowledge.PublishArtifactAsync(
            new KnowledgeDescriptor(
                article.Identity.Title,
                article.Content.Abstract,
                article.Identity.Subtitle,
                $"Canonical article {article.Identity.CanonicalId} revision {article.Publication.Revision}"),
            [
                new KnowledgeRepresentation(
                    ArticleContentType,
                    content.LongLength,
                    _ => Task.FromResult<Stream>(
                        new MemoryStream(content, writable: false)),
                    encoding: "utf-8"),
            ],
            previousRevision is null ? null : [previousRevision],
            _authority,
            cancellationToken);

        await _knowledge.SetAuthoritativeReferenceAsync(
            CreateCanonicalReference(article.Identity.CanonicalId),
            artifact.Reference,
            cancellationToken);

        return article;
    }

    private async Task<IKnowledgeArtifact?> GetArtifactAsync(
        Guid canonicalId,
        CancellationToken cancellationToken)
        => await _knowledge.ResolveReferenceAsync(
            CreateCanonicalReference(canonicalId),
            cancellationToken);

    private AuthoritativeReference CreateCanonicalReference(Guid canonicalId)
        => new(
            _options.KnowledgeScheme,
            ArticleKind,
            canonicalId.ToString("N"),
            SchemaVersion,
            _authority,
            CanonicalRole);

    private async Task<Article> ReadArticleAsync(
        IKnowledgeArtifact artifact,
        CancellationToken cancellationToken)
    {
        var representation = artifact.Representations.FirstOrDefault(candidate =>
            string.Equals(
                candidate.ContentType,
                ArticleContentType,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                $"Knowledge artifact '{artifact.Reference}' is not an article.");

        await using var stream = await representation.OpenStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<Article>(
                   stream,
                   _serializerOptions,
                   cancellationToken)
               ?? throw new InvalidDataException(
                   $"Article artifact '{artifact.Reference}' is empty or invalid.");
    }

    private static bool IsArticleArtifact(IKnowledgeArtifact artifact)
        => artifact.Representations.Any(candidate =>
            string.Equals(
                candidate.ContentType,
                ArticleContentType,
                StringComparison.OrdinalIgnoreCase));

    private void ValidateArticle(Article article)
    {
        if (article.Identity.CanonicalId == Guid.Empty)
        {
            throw new ArgumentException("Canonical ID is required.", nameof(article));
        }

        if (string.IsNullOrWhiteSpace(article.Identity.Title))
        {
            throw new ArgumentException("Title is required.", nameof(article));
        }

        if (string.IsNullOrWhiteSpace(article.Identity.Slug))
        {
            throw new ArgumentException("Slug is required.", nameof(article));
        }

        if (article.Publication.Revision < 1)
        {
            throw new ArgumentException("Revision must be at least 1.", nameof(article));
        }

        if (article.Provenance.UpdatedAtUtc < article.Provenance.CreatedAtUtc)
        {
            throw new ArgumentException(
                "Updated time cannot precede created time.",
                nameof(article));
        }
    }

    private void EnsureAuthority(Article article)
    {
        if (!string.Equals(
                article.Provenance.Authority.Id,
                _options.Authority.Id,
                StringComparison.Ordinal) ||
            !string.Equals(
                article.Provenance.Authority.Name,
                _options.Authority.Name,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Article authority must be '{_options.Authority.Name}' ({_options.Authority.Id}).");
        }
    }

    private static ArticleLibraryOptions ValidateOptions(ArticleLibraryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.KnowledgeScheme))
        {
            throw new ArgumentException("Knowledge scheme is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Authority.Id) ||
            string.IsNullOrWhiteSpace(options.Authority.Name))
        {
            throw new ArgumentException("Library authority is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.AuthorityContext))
        {
            throw new ArgumentException("Authority context is required.", nameof(options));
        }

        return options;
    }

    private static void EnsureCanonicalId(Guid canonicalId)
    {
        if (canonicalId == Guid.Empty)
        {
            throw new ArgumentException(
                "Canonical ID is required.",
                nameof(canonicalId));
        }
    }
}
