using AethericForge.Runtime.Models.Library.Articles;

namespace AethericForge.Runtime.Services.Library;

public sealed record ArticleLibraryOptions
{
    public const string DefaultAuthorityContext = "library:articles";

    /// <summary>
    /// Scheme of the Knowledge provider in which canonical references are stored.
    /// </summary>
    public required string KnowledgeScheme { get; init; }

    public required InstitutionReference Authority { get; init; }

    public InstitutionReference? DefaultOrigin { get; init; }

    public string AuthorityContext { get; init; } = DefaultAuthorityContext;
}
