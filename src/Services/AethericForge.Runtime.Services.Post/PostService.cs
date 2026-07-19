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
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using AethericForge.Runtime.Institutions.PostOffice;
using AethericForge.Runtime.Models.Post;

namespace AethericForge.Runtime.Services.Post;

public sealed class PostService : IPostService, IDisposable
{
    private readonly IReadOnlyDictionary<string, IPostProvider> _providers;
    private readonly IPostExchange _exchange;
    private readonly IDisposable _workbenchSubscription;

    public PostService(
        IEnumerable<IPostProvider> providers,
        IPostExchange exchange,
        IWorkbenchService workbench)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        ArgumentNullException.ThrowIfNull(workbench);

        _providers = providers.ToDictionary(provider => provider.Name, StringComparer.Ordinal);

        if (_providers.Count == 0)
        {
            throw new ArgumentException("At least one post provider is required.", nameof(providers));
        }

        _workbenchSubscription = workbench.Subscribe<IPostEnvelope>(PublishAsync);
    }

    public Task<IPostReference> AcceptAsync(
        IPostEnvelope envelope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return _exchange.AcceptAsync(envelope, ct);
    }

    public Task<IPostEnvelope?> CollectAsync(
        IPostReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return _exchange.CollectAsync(reference, ct);
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

    public void Dispose()
    {
        _workbenchSubscription.Dispose();
    }

    private Task PublishAsync(
        IPostEnvelope envelope,
        CancellationToken ct)
    {
        return GetProvider(envelope.Reference).PublishAsync(envelope, ct);
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
