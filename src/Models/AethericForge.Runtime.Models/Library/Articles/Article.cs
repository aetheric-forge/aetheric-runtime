namespace AethericForge.Runtime.Models.Library.Articles;

/// <summary>
/// The Library's authoritative, editor-facing record of an article.
/// </summary>
public sealed record Article
{
    public required ArticleIdentity Identity { get; init; }

    public ArticleAuthorship Authorship { get; init; } = new();

    public ArticlePublication Publication { get; init; } = new();

    public ArticleClassification Classification { get; init; } = new();

    public ArticleContent Content { get; init; } = new();

    public required ArticleProvenance Provenance { get; init; }
}

public sealed record ArticleIdentity
{
    /// <summary>
    /// Stable Library identifier. It does not change when the title or slug changes.
    /// </summary>
    public Guid CanonicalId { get; init; } = Guid.NewGuid();

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public string? Subtitle { get; init; }
}

public sealed record ArticleAuthorship
{
    public IReadOnlyList<ArticleAgent> Authors { get; init; } = [];

    /// <summary>
    /// Institution under whose auspices the work was written.
    /// This is distinct from the Library that holds authority over the record.
    /// </summary>
    public InstitutionReference? Institution { get; init; }

    public IReadOnlyList<ArticleContribution> Contributors { get; init; } = [];
}

public sealed record ArticleAgent
{
    /// <summary>
    /// Stable identity reference when the agent is known to the Forge.
    /// </summary>
    public string? IdentityReference { get; init; }

    public required string DisplayName { get; init; }

    public string? Orcid { get; init; }
}

public sealed record ArticleContribution
{
    public required ArticleAgent Contributor { get; init; }

    /// <summary>
    /// Human-readable contribution role, for example editor, illustrator, or reviewer.
    /// </summary>
    public required string Role { get; init; }
}

public sealed record InstitutionReference
{
    public required string Id { get; init; }

    public required string Name { get; init; }
}

public sealed record ArticlePublication
{
    public ArticlePublicationState State { get; init; } = ArticlePublicationState.Draft;

    public ArticleVisibility Visibility { get; init; } = ArticleVisibility.Private;

    /// <summary>
    /// Monotonically increasing edition of the canonical article record.
    /// </summary>
    public int Revision { get; init; } = 1;

    public DateTimeOffset? PublishedAtUtc { get; init; }

    public DateTimeOffset? ScheduledForUtc { get; init; }
}

public enum ArticlePublicationState
{
    Draft = 0,
    InReview = 1,
    Approved = 2,
    Scheduled = 3,
    Published = 4,
    Withdrawn = 5,
    Archived = 6,
}

public enum ArticleVisibility
{
    Private = 0,
    Campus = 1,
    Unlisted = 2,
    Public = 3,
}

public sealed record ArticleClassification
{
    public IReadOnlyList<string> Topics { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? Series { get; init; }

    public string? Collection { get; init; }
}

public sealed record ArticleContent
{
    public string? Abstract { get; init; }

    /// <summary>
    /// Source text for the article. The format is identified by <see cref="BodyFormat"/>.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    public string BodyFormat { get; init; } = ArticleContentFormats.Markdown;

    public IReadOnlyList<ArticleNote> Notes { get; init; } = [];

    public IReadOnlyList<ArticleReference> References { get; init; } = [];
}

public static class ArticleContentFormats
{
    public const string Markdown = "text/markdown";
    public const string Html = "text/html";
    public const string PlainText = "text/plain";
}

public sealed record ArticleNote
{
    public required string Text { get; init; }

    public ArticleNoteAudience Audience { get; init; } = ArticleNoteAudience.Editorial;

    public string? CreatedBy { get; init; }

    public DateTimeOffset? CreatedAtUtc { get; init; }
}

public enum ArticleNoteAudience
{
    Editorial = 0,
    Author = 1,
    Public = 2,
}

public sealed record ArticleReference
{
    /// <summary>
    /// Citation text as entered by the author or editor.
    /// </summary>
    public required string Citation { get; init; }

    public Uri? Uri { get; init; }

    public string? Doi { get; init; }
}

public sealed record ArticleProvenance
{
    /// <summary>
    /// The institution with final authority over the canonical article record.
    /// Forge-authored articles should identify the Library here.
    /// </summary>
    public required InstitutionReference Authority { get; init; }

    /// <summary>
    /// The institution in which the article originated, such as the Forge campus.
    /// </summary>
    public InstitutionReference? Origin { get; init; }

    public required ArticleAgent CreatedBy { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public required ArticleAgent UpdatedBy { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<ArticleSourceEvent> SourceHistory { get; init; } = [];

    public Guid? Supersedes { get; init; }

    public Guid? SupersededBy { get; init; }
}

public sealed record ArticleSourceEvent
{
    public required string Source { get; init; }

    public required string Action { get; init; }

    public required ArticleAgent Actor { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? Detail { get; init; }
}
