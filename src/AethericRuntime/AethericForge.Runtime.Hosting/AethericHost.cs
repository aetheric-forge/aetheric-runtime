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

