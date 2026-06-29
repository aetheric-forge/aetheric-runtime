using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AethericForge.Runtime.Bus.Abstractions;

namespace AethericForge.Runtime.Bus.Transports;

/// <summary>
/// RabbitMQ-backed transport implementing topic-style routing.
/// Uses a topic exchange; each subscription creates a transient, exclusive queue
/// bound with the provided binding key (supports * and # like RabbitMQ topics).
/// </summary>
public sealed class RabbitMqTransport(
    string url,
    string exchangeName,
    bool durableExchange = true,
    bool autoDeleteExchange = false,
    bool durableQueues = true,
    bool exclusiveQueues = false,
    bool autoDeleteQueues = false,
    string? queueNamePrefix = null) : ITransport
{
    private IConnection? _conn;
    private IChannel? _channel;
    private volatile bool _started;
    private readonly ConcurrentQueue<(string pattern, EnvelopeHandler handler)> _pending = new();

    public async Task StartAsync(CancellationToken ct = default)
    {
        var factory = new ConnectionFactory
        {
            Uri = new(url),
            ConsumerDispatchConcurrency = 4
        };

        _conn = await factory.CreateConnectionAsync(ct);
        _channel = await _conn.CreateChannelAsync(cancellationToken: ct);
        // Non-durable, auto-delete exchange suitable for tests
        await _channel.ExchangeDeclareAsync(
            exchangeName,
            ExchangeType.Topic,
            durable: durableExchange,
            autoDelete: autoDeleteExchange,
            cancellationToken: ct);
        _started = true;

        // drain any pending subscriptions that were registered before Start()
        while (_pending.TryDequeue(out var sub))
        {
            await InternalSubscribe(sub.pattern, sub.handler);
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        _started = false;

        // Take local copies to avoid weirdness if Stop() is called twice.
        var channel = _channel;
        var conn = _conn;

        // Clear fields early so anything racing against Stop()
        // will see "no longer usable".
        _channel = null;
        _conn = null;

        if (channel is not null)
        {
            try { await channel.CloseAsync(ct); } catch { /* ignore */ }
            channel.Dispose();
        }

        if (conn is not null)
        {
            try { await conn.CloseAsync(ct); } catch { /* ignore */ }
            conn.Dispose();
        }
    }

    public async Task PublishAsync(Envelope envelope, CancellationToken ct = default)
    {
        if (!_started || _channel is null)
            throw new InvalidOperationException("Transport not started");

        var json = JsonSerializer.Serialize(envelope);
        var body = Encoding.UTF8.GetBytes(json);

        await _channel.BasicPublishAsync(
             exchange: exchangeName,
             routingKey: ResolveRoutingKey(envelope),
             body: body,
             cancellationToken: ct
         );
    }

    public async Task SubscribeAsync(RouteKey routeKey, EnvelopeHandler handler, CancellationToken ct = default)
    {
        if (!_started || _channel is null)
        {
            _pending.Enqueue((ResolveRoutingKey(routeKey), handler));
            return; // will be bound on Start()
        }

        await InternalSubscribe(ResolveRoutingKey(routeKey), handler, ct);
    }


    private static readonly JsonSerializerOptions EnvelopeJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private async Task InternalSubscribe(string pattern, EnvelopeHandler handler, CancellationToken ct = default)
    {
        if (_channel is null) return;

        var queueName = queueNamePrefix is null
            ? $"aetheric-{exchangeName}-{pattern}-{Guid.NewGuid():N}".Replace("*", "_star_").Replace("#", "_hash_")
            : BuildQueueName(queueNamePrefix, pattern);

        var queue = await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: durableQueues,
            exclusive: exclusiveQueues,
            autoDelete: autoDeleteQueues,
            cancellationToken: ct);

        ct.ThrowIfCancellationRequested();

        await _channel.QueueBindAsync(
            queue: queue.QueueName,
            exchange: exchangeName,
            routingKey: pattern,
            cancellationToken: ct);

        ct.ThrowIfCancellationRequested();

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                // 1) Decode JSON
                var json = Encoding.UTF8.GetString(ea.Body.Span);

                // 2) Deserialize Envelope
                var envelope = JsonSerializer.Deserialize<Envelope>(json, EnvelopeJson)
                               ?? throw new InvalidOperationException("Failed to deserialize Envelope.");

                // 3) Optional: validate structural invariants
                EnvelopeValidator.Validate(envelope);

                // 4) Hand off to handler
                await handler(envelope, ct);

                // 5) Ack only after successful handler completion
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch
            {
                // In tests: reject without requeue to avoid infinite loops
                await _channel.BasicRejectAsync(ea.DeliveryTag, requeue: false);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queue.QueueName,
            autoAck: false,
            consumer: consumer);
    }

    private static string ResolveRoutingKey(RouteKey routeKey) =>
        routeKey.Kind switch
        {
            EnvelopeKind.Request => $"{routeKey.Service}.{routeKey.Verb}",
            EnvelopeKind.Event => routeKey.Topic ?? throw new InvalidOperationException("Event requires Topic."),
            EnvelopeKind.Response or EnvelopeKind.Error =>
                throw new InvalidOperationException("Response/Error routing requires client_id metadata."),
            _ => throw new InvalidOperationException($"Unknown envelope kind: {routeKey.Kind}")
        };

    private static string ResolveRoutingKey(Envelope envelope) =>
        envelope.Kind switch
        {
            EnvelopeKind.Request => $"{envelope.Service}.{envelope.Verb}",
            EnvelopeKind.Event => envelope.Topic ?? throw new InvalidOperationException("Event requires Topic."),
            EnvelopeKind.Response or EnvelopeKind.Error =>
                $"reply.{envelope.Meta["client_id"]}",
            _ => throw new InvalidOperationException($"Unknown envelope kind: {envelope.Kind}")
        };

    private static string BuildQueueName(string prefix, string pattern) =>
        $"{prefix}.{pattern}"
            .Replace("*", "_star_")
            .Replace("#", "_hash_")
            .Replace("..", ".");
}
