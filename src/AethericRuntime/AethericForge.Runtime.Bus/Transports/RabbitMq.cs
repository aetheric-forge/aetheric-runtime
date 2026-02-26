using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Util;

namespace AethericForge.Runtime.Bus.Transports;

/// <summary>
/// RabbitMQ-backed transport implementing topic-style routing.
/// Uses a topic exchange; each subscription creates a transient, exclusive queue
/// bound with the provided binding key (supports * and # like RabbitMQ topics).
/// </summary>
public sealed class RabbitMqTransport(string url, string exchangeName) : ITransport
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
        await _channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: false, autoDelete: true, cancellationToken: ct);
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
             routingKey: RoutingHelpers.ResolveRoutingKey(envelope),
             body: body,
             cancellationToken: ct
         );
    }

    public async Task SubscribeAsync(string pattern, EnvelopeHandler handler, CancellationToken ct = default)
    {
        if (!_started || _channel is null)
        {
            _pending.Enqueue((pattern, handler));
            return; // will be bound on Start()
        }

        await InternalSubscribe(pattern, handler, ct);
    }


    private static readonly JsonSerializerOptions EnvelopeJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private async Task InternalSubscribe(string pattern, EnvelopeHandler handler, CancellationToken ct = default)
    {
        if (_channel is null) return;

        // Create an exclusive, auto-delete queue per handler
        var queue = await _channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: ct);

        ct.ThrowIfCancellationRequested();

        await _channel.QueueBindAsync(
            queue: queue.QueueName,
            exchange: exchangeName,
            routingKey: pattern,
            cancellationToken: ct);

        ct.ThrowIfCancellationRequested();

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
}
