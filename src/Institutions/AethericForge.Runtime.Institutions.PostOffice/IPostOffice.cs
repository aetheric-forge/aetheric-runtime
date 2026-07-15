using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

namespace AethericForge.Runtime.Institutions.PostOffice;

/// <summary>
/// Represents an Institution that exchanges post within an institutional
/// hierarchy.
/// </summary>
public interface IPostOffice : IInstitution
{
    /// <summary>
    /// Accepts an envelope into the postal exchange.
    /// </summary>
    /// <param name="envelope">
    /// The envelope containing the post and its routing information.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A reference to the accepted post.</returns>
    Task<IPostReference> AcceptAsync(
        IPostEnvelope envelope,
        CancellationToken ct = default);

    /// <summary>
    /// Collects the envelope identified by a post reference.
    /// </summary>
    /// <param name="reference">
    /// The reference identifying the requested post.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The corresponding envelope, or null when it is unavailable from this
    /// exchange.
    /// </returns>
    Task<IPostEnvelope?> CollectAsync(
        IPostReference reference,
        CancellationToken ct = default);
}