using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Models.Post;

namespace AethericForge.Runtime.Services.Post;

public sealed class PostService : IPostService
{
    private readonly IReadOnlyDictionary<string, IPostProvider> _providers;

    public PostService(IEnumerable<IPostProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToDictionary(provider => provider.Name, StringComparer.Ordinal);

        if (_providers.Count == 0)
        {
            throw new ArgumentException("At least one post provider is required.", nameof(providers));
        }
    }

    public Task PublishAsync<TMessage>(
        IPostReference reference,
        TMessage message,
        IPostMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        var envelope = new PostEnvelope<TMessage>(
            reference,
            message,
            metadata ?? new PostMetadata());

        return GetProvider(reference).PublishAsync(envelope, ct);
    }

    public Task SubscribeAsync<TMessage>(
        IPostReference reference,
        IMessageConsumer<TMessage> consumer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(consumer);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).SubscribeAsync(reference, consumer, ct);
    }

    private IPostProvider GetProvider(IPostReference reference)
    {
        return GetProvider(reference.Domain);
    }

    private IPostProvider GetProvider(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Domain is required.", nameof(domain));
        }

        if (_providers.TryGetValue(domain.Trim(), out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"No post provider is registered for domain '{domain}'.");
    }
}
