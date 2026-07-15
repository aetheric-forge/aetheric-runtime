using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Post.Services;


/// <summary>
/// Provides high-level postal services for sending and receiving objects,
/// handling serialization and routing metadata automatically.
/// </summary>

/// <summary>
/// Provides high-level postal services for sending and receiving post.
/// </summary>
public interface IPostmaster : IAuthority<IPostClerk>
{
    /// <summary>
    /// Sends a post envelope.
    /// </summary>
    /// <param name="envelope">
    /// The envelope containing the post and its routing information.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A reference to the posted envelope.</returns>
    Task<IPostReference> SendAsync(
        IPostEnvelope envelope,
        CancellationToken ct = default);

    /// <summary>
    /// Receives the post envelope identified by a reference.
    /// </summary>
    /// <param name="reference">
    /// The reference identifying the posted envelope.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The corresponding post envelope, or null if it is unavailable.
    /// </returns>
    Task<IPostEnvelope?> ReceiveAsync(
        IPostReference reference,
        CancellationToken ct = default);
}