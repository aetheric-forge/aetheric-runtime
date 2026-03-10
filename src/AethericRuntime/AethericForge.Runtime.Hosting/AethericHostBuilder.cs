using AethericForge.Runtime.Bus;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Util;

namespace AethericForge.Runtime.Hosting;

public sealed class AethericHostBuilder(string serviceName = "default")
{
    private readonly string _name = serviceName;
    private readonly List<ITransport> _transports = new();
    private readonly List<(Type payloadType, RouteKey routeKey, MessageHandler handler)> _handlers = new();
    private readonly List<(RouteKey routeKey, EnvelopeHandler handler)> _routes = new();
    private readonly Dictionary<Type, object> _repos = new();

    private ITransport? _primaryTransport;

    private static (Type messageType, Type closedInterface)? ResolveClosedGeneric(Type concreteType, Type openGenericInterface)
    {
        // Finds e.g. ICommandHandler<CreateOrder>
        var hit = concreteType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface);

        if (hit is null) return null;

        var args = hit.GetGenericArguments();
        return (args[0], hit);
    }

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
    public AethericHostBuilder AddHandler<TPayload>(RouteKey routeKey, MessageHandler handler)
    {
        _routes.Add((routeKey, (envelope, ct) =>
                {
                    return Task.CompletedTask;
                }
        ));

        _handlers.Add((typeof(TPayload), routeKey, async (payload, ctx) =>
        {
            await handler(payload, ctx);
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
        foreach (var (routeKey, handler) in _routes)
            broker.Route(routeKey, handler);

        // Convert registered handler *types* into executable delegates.
        foreach (var transport in _transports)
        {
            foreach (var (payloadType, handler) in _handlers)
            {
                ct.ThrowIfCancellationRequested();

                await transport.SubscribeAsync(
                    routingKey,
                    async (envelope, ct) =>
                    {
                        var ctx = new MessageContext(envelope, _name, broker, _repos, ct);
                        if (!payloadType.IsInstanceOfType(envelope.UntypedPayload))
                            throw new InvalidOperationException(
                                $"Envelope payload type mismatch. Expected {payloadType.Name}.");
                        await handler(envelope.UntypedPayload, ctx);
                    }, ct);
            }
        }
        return new AethericHost(_name, _transports, broker, _repos);
    }

    public AethericHostBuilder UseModule<TModule>()
        where TModule : IAethericModule, new()
    {
        var module = new TModule();
        module.Register(this);
        return this;
    }

    public AethericHostBuilder AddCommandHandler(Type handlerType)
    {
        if (handlerType is null) throw new ArgumentNullException(nameof(handlerType));

        var (commandType, _) = ResolveClosedGeneric(handlerType, typeof(ICommandHandler<>))
            ?? throw new InvalidOperationException(
                $"{handlerType.FullName} does not implement ICommandHandler<T>.");

        if (_commandHandlers.TryGetValue(commandType, out var existing))
            throw new InvalidOperationException(
                $"Command {commandType.FullName} already has handler {existing.FullName}. Only one is allowed.");

        _commandHandlers[commandType] = handlerType;
        return this;
    }

    public AethericHostBuilder AddEventHandler(Type handlerType)
    {
        if (handlerType is null) throw new ArgumentNullException(nameof(handlerType));

        var (eventType, _) = ResolveClosedGeneric(handlerType, typeof(IEventHandler<>))
            ?? throw new InvalidOperationException(
                $"{handlerType.FullName} does not implement IEventHandler<T>.");

        if (!_eventHandlers.TryGetValue(eventType, out var list))
        {
            list = new List<Type>();
            _eventHandlers[eventType] = list;
        }

        list.Add(handlerType);
        return this;
    }

    /// <summary>
    /// Scans assemblies for ICommandHandler&lt;&gt; and IEventHandler&lt;&gt; types whose Namespace starts with nsPrefix.
    /// </summary>
    public AethericHostBuilder AddHandlersFromNamespace(
        string nsPrefix,
        params System.Reflection.Assembly[]? assemblies)
    {
        if (string.IsNullOrWhiteSpace(nsPrefix))
            throw new ArgumentException("Namespace prefix is required.", nameof(nsPrefix));

        var asms = (assemblies is { Length: > 0 })
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        foreach (var asm in asms)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray(); }

            foreach (var t in types)
            {
                if (t is null) continue;
                if (!t.IsClass || t.IsAbstract) continue;

                var ns = t.Namespace ?? string.Empty;
                if (!ns.StartsWith(nsPrefix, StringComparison.Ordinal)) continue;

                if (ResolveClosedGeneric(t, typeof(ICommandHandler<>)) is not null)
                    AddCommandHandler(t);

                if (ResolveClosedGeneric(t, typeof(IEventHandler<>)) is not null)
                    AddEventHandler(t);
            }
        }

        return this;
    }
}

