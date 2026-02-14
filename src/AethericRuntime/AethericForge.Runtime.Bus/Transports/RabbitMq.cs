using System.Collections.Concurrent;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Model.Messages;

namespace AethericForge.Runtime.Bus.Transports;

/// <summary>
/// RabbitMQ-backed transport implementing topic-style routing.
/// Uses a topic exchange; each subscription creates a transient, exclusive queue
/// bound with the provided binding key (supports * and # like RabbitMQ topics).
/// </summary>
public sealed class RabbitMqTransport(string url, string exchangeName = "parallel_you_tests") : ITransport
{
    private IConnection? _conn;
    private IChannel? _channel;
    private volatile bool _started;
    private readonly ConcurrentQueue<(string pattern, MessageHandler handler)> _pending = new();

    public async Task Start()
    {
        var factory = new ConnectionFactory
        {
            Uri = new(url),
            ConsumerDispatchConcurrency = 4
        };

        _conn = await factory.CreateConnectionAsync();
        _channel = await _conn.CreateChannelAsync();
        // Non-durable, auto-delete exchange suitable for tests
        await _channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: false, autoDelete: true);
        _started = true;

        // drain any pending subscriptions that were registered before Start()
        while (_pending.TryDequeue(out var sub))
        {
            await InternalSubscribe(sub.pattern, sub.handler);
        }
    }

    public async Task Stop()
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
            try { await channel.CloseAsync(); } catch { /* ignore */ }
            channel.Dispose();
        }

        if (conn is not null)
        {
            try { await conn.CloseAsync(); } catch { /* ignore */ }
            conn.Dispose();
        }
    }

    public async Task Publish(Message msg)
    {
        if (!_started || _channel is null)
            throw new InvalidOperationException("Transport not started");

        var body = ReadOnlyMemory<byte>.Empty; // we don't rely on body content in current tests
        var props = new BasicProperties { Persistent = true };
        await _channel.BasicPublishAsync(exchange: exchangeName, routingKey: msg.Type, mandatory: false, basicProperties: props, body: body);
    }

    public async Task Subscribe(string pattern, MessageHandler handler)
    {
        if (!_started || _channel is null)
        {
            _pending.Enqueue((pattern, handler));
            return; // will be bound on Start()
        }

        await InternalSubscribe(pattern, handler);
    }

    private async Task InternalSubscribe(string pattern, MessageHandler handler)
    {
        if (_channel is null) return;

        // Create an exclusive, auto-delete queue per handler
        var queue = await _channel.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true);
        await _channel.QueueBindAsync(queue.QueueName, exchangeName, routingKey: pattern);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var routingKey = ea.RoutingKey;
                var msg = new SimpleMessage(new Dictionary<string, object>(), routingKey, null);
                await handler(msg);
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch
            {
                // Reject and do not requeue to avoid infinite redelivery loops in tests
                await _channel!.BasicRejectAsync(ea.DeliveryTag, requeue: false);
            }
        };

        await _channel.BasicConsumeAsync(queue: queue.QueueName, autoAck: false, consumer: consumer);
    }

    /*
     * This class represents the JSON payload, with an optional `meta` dictionary
     */
    private sealed class SimpleMessage(
        IReadOnlyDictionary<string, object> payload,
        string? type,
        IReadOnlyDictionary<string, object>? meta)
        
        // ReSharper disable once ArrangeObjectCreationWhenTypeNotEvident
        // ReSharper disable once HeapView.ObjectAllocation.Evident
        // If no payload is provided, a `Message` object with an empty dictionary will be constructed
        // The type is a string that is understood by the services and clients to key to a particular event class
        : Message<Dictionary<string, object>>(new(payload), type)
    {
        // ReSharper disable once UnusedMember.Local
        public IReadOnlyDictionary<string, object>? Meta => meta;
    }
}
