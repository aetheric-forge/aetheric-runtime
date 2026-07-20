using AethericForge.Runtime.Models.Library.Articles;

namespace AethericForge.Runtime.Services.Library;

public static class ArticleWorkflow
{
    private static readonly IReadOnlyDictionary<ArticlePublicationState, ArticlePublicationState[]> AllowedTransitions =
        new Dictionary<ArticlePublicationState, ArticlePublicationState[]>
        {
            [ArticlePublicationState.Draft] =
                [ArticlePublicationState.InReview],
            [ArticlePublicationState.InReview] =
                [ArticlePublicationState.Draft, ArticlePublicationState.Approved],
            [ArticlePublicationState.Approved] =
                [ArticlePublicationState.Draft, ArticlePublicationState.Scheduled, ArticlePublicationState.Published],
            [ArticlePublicationState.Scheduled] =
                [ArticlePublicationState.Draft, ArticlePublicationState.Published],
            [ArticlePublicationState.Published] =
                [ArticlePublicationState.Withdrawn, ArticlePublicationState.Archived],
            [ArticlePublicationState.Withdrawn] =
                [ArticlePublicationState.Draft, ArticlePublicationState.Archived],
            [ArticlePublicationState.Archived] = [],
        };

    public static bool CanTransition(
        ArticlePublicationState currentState,
        ArticlePublicationState targetState)
        => AllowedTransitions[currentState].Contains(targetState);

    public static void EnsureCanTransition(
        ArticlePublicationState currentState,
        ArticlePublicationState targetState)
    {
        if (!CanTransition(currentState, targetState))
        {
            throw new InvalidOperationException(
                $"An article cannot transition from {currentState} to {targetState}.");
        }
    }
}
