using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

namespace AethericForge.Runtime.Institutions.PostOffice;

/// <summary>
/// Provides the operational exchange used by a Post Office.
/// </summary>
public interface IPostExchange
{
    Task<IPostReference> AcceptAsync(
        IPostEnvelope envelope,
        CancellationToken ct = default);

    Task<IPostEnvelope?> CollectAsync(
        IPostReference reference,
        CancellationToken ct = default);
}