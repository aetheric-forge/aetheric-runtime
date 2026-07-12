using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Models.Post;

namespace AethericForge.Runtime.Providers.Post.InMemory;

public sealed class InMemoryPostProvider : IPostProvider
{
    private readonly object _sync = new();
    private readonly Dictionary<PostRouteKey, List<IMessageConsumer>> _subscriptions = new();

    public InMemoryPostProvider(string name)
    {
        Name = NormalizeRequired(name, nameof(name));
    }

    public string Name { get; }

    public async Task PublishAsync(
        IPostEnvelope envelope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        EnsureOwns(envelope.Reference);
        ct.ThrowIfCancellationRequested();

        var routeKey = PostRouteKey.From(envelope.Reference);
        IMessageConsumer[] subscribers;

        lock (_sync)
        {
            subscribers = _subscriptions.TryGetValue(routeKey, out var current)
                ? current.ToArray()
                : [];
        }

        var context = new InMemoryPostContext(this, envelope);

        foreach (var subscriber in subscribers)
        {
            ct.ThrowIfCancellationRequested();
            await subscriber.ConsumeAsync(envelope, context, ct);
        }
    }

    public Task SubscribeAsync(
        IPostReference reference,
        IMessageConsumer consumer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(consumer);
        EnsureOwns(reference);
        EnsureCompatibleContract(reference.Contract, consumer.Contract);
        ct.ThrowIfCancellationRequested();

        var routeKey = PostRouteKey.From(reference);

        lock (_sync)
        {
            if (!_subscriptions.TryGetValue(routeKey, out var subscribers))
            {
                subscribers = [];
                _subscriptions[routeKey] = subscribers;
            }

            subscribers.Add(consumer);
        }

        return Task.CompletedTask;
    }

    private void EnsureOwns(IPostReference reference)
    {
        if (!string.Equals(Name, reference.Domain, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Provider domain '{Name}' cannot handle reference for domain '{reference.Domain}'.");
        }
    }

    private static void EnsureCompatibleContract(
        IPostContract referenceContract,
        IPostContract consumerContract)
    {
        if (!PostRouteKey.ContractEquals(referenceContract, consumerContract))
        {
            throw new ArgumentException(
                "Consumer contract must match the subscription reference contract.",
                nameof(consumerContract));
        }
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }

    private sealed class InMemoryPostContext : IPostContext
    {
        private readonly InMemoryPostProvider _provider;

        public InMemoryPostContext(
            InMemoryPostProvider provider,
            IPostEnvelope envelope)
        {
            _provider = provider;
            Envelope = envelope;
            Attributes = envelope.Metadata.Attributes;
        }

        public IPostEnvelope Envelope { get; }
        public IReadOnlyDictionary<string, string> Attributes { get; }

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
                metadata ?? new PostMetadata(
                    correlationId: Envelope.Metadata.CorrelationId ?? Envelope.Metadata.MessageId,
                    causationId: Envelope.Metadata.MessageId));

            return _provider.PublishAsync(envelope, ct);
        }
    }

    private sealed record PostRouteKey(
        string Domain,
        string Address,
        string ContractName,
        string ContractVersion,
        PostIntent ContractIntent,
        string Qualifiers)
    {
        public static PostRouteKey From(IPostReference reference)
        {
            return new PostRouteKey(
                reference.Domain,
                reference.Address,
                reference.Contract.Name,
                reference.Contract.Version,
                reference.Contract.Intent,
                CreateQualifiersKey(reference.Qualifiers));
        }

        public static bool ContractEquals(
            IPostContract left,
            IPostContract right)
        {
            return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                   && string.Equals(left.Version, right.Version, StringComparison.Ordinal)
                   && left.Intent == right.Intent;
        }

        private static string CreateQualifiersKey(IReadOnlyDictionary<string, string> qualifiers)
        {
            if (qualifiers.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(
                '\n',
                qualifiers
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => $"{x.Key}={x.Value}"));
        }
    }
}
