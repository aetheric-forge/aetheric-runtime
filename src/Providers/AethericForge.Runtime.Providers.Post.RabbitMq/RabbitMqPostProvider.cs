using System.Text;
using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Models.Post;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AethericForge.Runtime.Providers.Post.RabbitMq;

public sealed class RabbitMqPostProvider : IPostProvider, IAsyncDisposable
{
    private readonly string _name;
    private readonly ConnectionFactory _connectionFactory;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly string _exchangeName;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RabbitMqPostProvider(string name, string connectionString)
    {
        _name = name;
        _connectionFactory = new ConnectionFactory { Uri = new Uri(connectionString) };
        _exchangeName = $"aetheric.post.{_name}";
    }

    public string Name => _name;

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel != null) return _channel;

        await _lock.WaitAsync(ct);
        try
        {
            if (_channel != null) return _channel;

            _connection ??= await _connectionFactory.CreateConnectionAsync(cancellationToken: ct);
            _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

            await _channel.ExchangeDeclareAsync(
                exchange: _exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: ct);

            return _channel;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PublishAsync(IPostEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        EnsureOwns(envelope.Reference);

        var channel = await GetChannelAsync(ct);

        var routingKey = GetRoutingKey(envelope.Reference);
        var body = Serialize(envelope);

        var properties = new BasicProperties
        {
            MessageId = envelope.Metadata.MessageId,
            CorrelationId = envelope.Metadata.CorrelationId,
            Timestamp = new AmqpTimestamp(envelope.Metadata.ProducedAtUtc.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>()
        };

        if (envelope.Metadata.CausationId != null)
        {
            properties.Headers["x-causation-id"] = envelope.Metadata.CausationId;
        }

        foreach (var attr in envelope.Metadata.Attributes)
        {
            properties.Headers[attr.Key] = attr.Value;
        }

        await channel.BasicPublishAsync(
            exchange: _exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);
    }

    public async Task SubscribeAsync(IPostReference reference, IMessageConsumer consumer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(consumer);
        EnsureOwns(reference);

        var channel = await GetChannelAsync(ct);
        var routingKey = GetRoutingKey(reference);

        // Queue name based on domain, address and contract to allow multiple instances of same service to share queue
        var queueName = $"{_exchangeName}.{routingKey}.{consumer.GetType().Name}";

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: _exchangeName,
            routingKey: routingKey,
            cancellationToken: ct);

        var rabbitConsumer = new AsyncEventingBasicConsumer(channel);
        rabbitConsumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var envelope = Deserialize(ea.Body.ToArray(), ea.BasicProperties, reference, consumer);
                var context = new RabbitMqPostContext(this, envelope);

                await consumer.ConsumeAsync(envelope, context, ct);
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            }
            catch
            {
                await channel.BasicNackAsync(ea.DeliveryTag, false, true, ct);
            }
        };

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: rabbitConsumer,
            cancellationToken: ct);
    }

    private void EnsureOwns(IPostReference reference)
    {
        if (!string.Equals(Name, reference.Domain, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Provider domain '{Name}' cannot handle reference for domain '{reference.Domain}'.");
        }
    }

    private string GetRoutingKey(IPostReference reference)
    {
        return $"{reference.Address}.{reference.Contract.Name}.{reference.Contract.Version}.{reference.Contract.Intent}".ToLowerInvariant();
    }

    private byte[] Serialize(IPostEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope.Payload);
        return Encoding.UTF8.GetBytes(json);
    }

    private IPostEnvelope Deserialize(byte[] body, IReadOnlyBasicProperties properties, IPostReference reference, IMessageConsumer consumer)
    {
        var json = Encoding.UTF8.GetString(body);
        var payloadType = GetPayloadType(consumer);
        var payload = JsonSerializer.Deserialize(json, payloadType);

        var headers = properties.Headers ?? new Dictionary<string, object?>();
        var attributes = headers.ToDictionary(
            k => k.Key, 
            v => v.Value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : v.Value?.ToString() ?? "");

        var metadata = new PostMetadata(
            messageId: properties.MessageId,
            correlationId: properties.CorrelationId,
            causationId: attributes.TryGetValue("x-causation-id", out var cid) ? cid : null,
            producedAtUtc: properties.Timestamp.UnixTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime) : DateTimeOffset.UtcNow,
            attributes: attributes
        );

        var envelopeType = typeof(PostEnvelope<>).MakeGenericType(payloadType);
        return (IPostEnvelope)Activator.CreateInstance(envelopeType, reference, payload, metadata)!;
    }

    private Type GetPayloadType(IMessageConsumer consumer)
    {
        var consumerType = consumer.GetType();
        var genericInterface = consumerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageConsumer<>));
        
        if (genericInterface != null)
        {
            return genericInterface.GetGenericArguments()[0];
        }

        return typeof(object);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }
        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
        _lock.Dispose();
    }

    private sealed class RabbitMqPostContext : IPostContext
    {
        private readonly RabbitMqPostProvider _provider;

        public RabbitMqPostContext(RabbitMqPostProvider provider, IPostEnvelope envelope)
        {
            _provider = provider;
            Envelope = envelope;
            Attributes = envelope.Metadata.Attributes;
        }

        public IPostEnvelope Envelope { get; }
        public IReadOnlyDictionary<string, string> Attributes { get; }

        public Task PublishAsync<TMessage>(IPostReference reference, TMessage message, IPostMetadata? metadata = null, CancellationToken ct = default)
        {
            var newEnvelope = new PostEnvelope<TMessage>(
                reference,
                message,
                metadata ?? new PostMetadata(
                    correlationId: Envelope.Metadata.CorrelationId ?? Envelope.Metadata.MessageId,
                    causationId: Envelope.Metadata.MessageId));

            return _provider.PublishAsync(newEnvelope, ct);
        }
    }
}
