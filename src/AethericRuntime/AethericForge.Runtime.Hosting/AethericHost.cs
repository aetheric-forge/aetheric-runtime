using AethericForge.Runtime.Bus;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Util;

namespace AethericForge.Runtime.Hosting;

/// <summary>
/// Minimal application host for the Aetheric Runtime.
/// 
/// Design goals:
/// - The host is orchestration, not business logic.
/// - Transports own their topology (e.g., each transport instance owns its exchange).
/// - Routing keys are semantic; exchange/queue names are infrastructure.
/// </summary>
public sealed class AethericHost : IAsyncDisposable
{
    private readonly string _name;
    private readonly List<ITransport> _transports;
    private readonly IBroker _broker;
    private readonly Dictionary<Type, object> _repos;

    internal AethericHost(string serviceName, List<ITransport> transports, IBroker broker, Dictionary<Type, object> repos)
    {
        _name = serviceName;
        _transports = transports;
        _broker = broker;
        _repos = repos;
    }

    public static AethericHostBuilder Create(string name = "default") => new(name);

    public IBroker Broker => _broker;

    public TRepo GetRepo<TRepo>() where TRepo : class
    {
        if (_repos.TryGetValue(typeof(TRepo), out var repo))
            return (TRepo)repo;

        throw new InvalidOperationException($"No repo registered for {typeof(TRepo).FullName}.");
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        foreach (var t in _transports)
            await t.StartAsync(ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        // Stop in reverse order, in case dependencies appear later.
        for (int i = _transports.Count - 1; i >= 0; i--)
            await _transports[i].StopAsync(ct);
    }

    /// <summary>
    /// Starts transports and then blocks until cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        await StartAsync(ct);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        finally
        {
            // Prefer a non-canceled token for shutdown work.
            await StopAsync(CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }
}

public sealed class AethericHostBuilder(string serviceName = "default")
{
    private readonly string _name = serviceName;
    private readonly List<ITransport> _transports = new();
    private readonly List<(string pattern, EnvelopeHandler handler)> _routes = new();
    private readonly Dictionary<Type, object> _repos = new();

    private ITransport? _primaryTransport;
    private readonly List<(Type PayloadType, string RoutingKey, Func<object, MessageContext, Task> Handler)> _handlers
        = new();

    /// <summary>
    /// Sets the default transport used for routing and publishing via <see cref="AethericHost.Broker"/>.
    /// You can still add more transports later if/when needed.
    /// </summary>
    public AethericHostBuilder UseTransport(ITransport transport)
    {
        _primaryTransport = transport;
        if (!_transports.Contains(transport))
            _transports.Add(transport);
        return this;
    }

    /// <summary>
    /// Registers an additional transport. (The host is transport-composable.)
    /// </summary>
    public AethericHostBuilder AddTransport(ITransport transport)
    {
        if (_primaryTransport is null)
            _primaryTransport = transport;
        if (!_transports.Contains(transport))
            _transports.Add(transport);
        return this;
    }

    /// <summary>
    /// Registers a repo instance by its abstraction type.
    /// </summary>
    public AethericHostBuilder UseRepo<TRepo>(TRepo repo) where TRepo : class
    {
        _repos[typeof(TRepo)] = repo;
        return this;
    }

    /// <summary>
    /// Adds a handler for envelopes matching a routing pattern.
    /// Handler receives a strongly-typed payload deserialized from <see cref="Envelope.Payload"/>.
    /// </summary>
    public AethericHostBuilder AddHandler<TPayload>(string pattern, Func<object, MessageContext, Task> handler)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern is required.", nameof(pattern));

        _handlers.Add((typeof(TPayload), pattern, async (payload, ctx) =>
        {
            await handler(payload, ctx);
        }
        ));

        return this;
    }

    /// <summary>
    /// Adds a handler using a default pattern convention: <c>slug(typeName)</c>.
    /// Example: <c>CreateThread</c> -> <c>create-thread</c>.
    /// 
    /// Useful for prototypes; for request routing, you will typically prefer
    /// explicit patterns like <c>threads.create</c>.
    /// </summary>
    public AethericHostBuilder AddHandler<TPayload>(Func<object, MessageContext, Task> handler)
    {
        var pattern = Slug.From(typeof(TPayload).Name);
        return AddHandler<TPayload>(pattern, handler);
    }

    /// <summary>
    /// Adds a low-level envelope handler.
    /// </summary>
    public AethericHostBuilder AddEnvelopeHandler(string pattern, EnvelopeHandler handler)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern is required.", nameof(pattern));

        _routes.Add((pattern, handler));
        return this;
    }

    public AethericHostBuilder AddCommandHandler<T>(
       Func<T, MessageContext, Task> handler)
    {
        var routingKey = RoutingHelpers.ResolveRoutingKey(
            EnvelopeKind.Request,
            _name,
            typeof(T).Name);

        _handlers.Add((
            typeof(T),
            routingKey,
            async (payload, ctx) =>
            {
                await handler((T)payload, ctx);
            }
        ));

        return this;
    }

    public AethericHostBuilder AddEventHandler<T>(
        Func<T, MessageContext, Task> handler)
    {
        var routingKey = RoutingHelpers.ResolveRoutingKey(
            EnvelopeKind.Event,
            _name,
            typeof(T).Name);

        _handlers.Add((
            typeof(T),
            routingKey,
            async (payload, ctx) =>
            {
                await handler((T)payload, ctx);
            }
        ));

        return this;
    }

    public async Task<AethericHost> BuildAsync(CancellationToken ct = default)
    {
        if (_primaryTransport is null)
            throw new InvalidOperationException("No transport configured. Call UseTransport(...) first.");

        var broker = new MessageBroker(_primaryTransport);

        // Register routes before starting transports. (Transport decides if late-subscribing is supported.)
        foreach (var (pattern, handler) in _routes)
            broker.Route(pattern, handler);

        foreach (var transport in _transports)
        {
            foreach (var (payloadType, routingKey, handler) in _handlers)
            {
                ct.ThrowIfCancellationRequested();

                await transport.SubscribeAsync(
                    routingKey,
                    async (envelope, ct) =>
                    {
                        var ctx = new MessageContext(envelope, _name, broker, ct);
                        if (!payloadType.IsInstanceOfType(envelope.UntypedPayload))
                            throw new InvalidOperationException(
                                $"Envelope payload type mismatch. Expected {payloadType.Name}.");
                        await handler(envelope.UntypedPayload, ctx);
                    }, ct);
            }
        }

        return new AethericHost(_name, _transports, broker, _repos);
    }
}

