using System.Collections.Concurrent;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Institutions.PostOffice;

namespace AethericForge.Runtime.Services.Post;

public sealed class PostExchange : IPostExchange
{
    private readonly ConcurrentDictionary<IPostReference, IPostEnvelope> _storage = new();

    public Task<IPostReference> AcceptAsync(IPostEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _storage.TryAdd(envelope.Reference, envelope);
        return Task.FromResult(envelope.Reference);
    }

    public Task<IPostEnvelope?> CollectAsync(IPostReference reference, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _storage.TryGetValue(reference, out var envelope);
        return Task.FromResult(envelope);
    }
}
