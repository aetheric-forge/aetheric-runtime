using AethericForge.Runtime.Models.Library.Articles;

namespace AethericForge.Runtime.Services.Library;

/// <summary>
/// Manages the Library's canonical article records and their publication workflow.
/// </summary>
public interface IArticleLibrary
{
    Task<Article?> GetAsync(
        Guid canonicalId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Article>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<Article> CreateAsync(
        Article article,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves editorial changes without changing publication state.
    /// </summary>
    Task<Article> SaveAsync(
        Article article,
        CancellationToken cancellationToken = default);

    Task<Article> TransitionAsync(
        Guid canonicalId,
        ArticlePublicationState targetState,
        ArticleAgent actor,
        DateTimeOffset? occurredAtUtc = null,
        CancellationToken cancellationToken = default);
}
