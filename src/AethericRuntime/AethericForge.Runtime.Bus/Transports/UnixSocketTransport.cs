using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AethericForge.Runtime.Bus.Abstractions;

namespace AethericForge.Runtime.Bus.Transports;

/// <summary>
/// Unix domain socket transport for local IPC.
/// Supports server mode (routes between multiple clients) and client mode (connects to a server).
/// </summary>
public sealed class UnixSocketTransport : ITransport, IAsyncDisposable
{
    private readonly UnixSocketTransportOptions _options;
    private readonly JsonSerializerOptions _json;

    // Local (in-process) subscriptions
    private readonly ConcurrentDictionary<string, ConcurrentBag<EnvelopeHandler>> _localHandlers = new();

    // Server state
    private Socket? _listenSocket;
    private readonly ConcurrentDictionary<Guid, ClientConnection> _clients = new();
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    // Client state (when ActAsServer == false)
    private ClientConnection? _clientConn;
    private readonly ConcurrentQueue<string> _pendingClientSubscriptions = new();

    public UnixSocketTransport(UnixSocketTransportOptions options, JsonSerializerOptions? jsonOptions = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.SocketPath))
            throw new ArgumentException("SocketPath must be set.", nameof(options));

        _json = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public Task SubscribeAsync(string pattern, EnvelopeHandler handler, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pattern)) throw new ArgumentException("Pattern must be provided.", nameof(pattern));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        // Always register locally so deliveries get handled in this process.
        var bag = _localHandlers.GetOrAdd(pattern, _ => new ConcurrentBag<EnvelopeHandler>());
        bag.Add(handler);

        // If we're a client, also inform the server so it routes deliveries to us.
        if (!_options.ActAsServer)
        {
            var conn = _clientConn;
            if (conn is null)
            {
                _pendingClientSubscriptions.Enqueue(pattern);
                return Task.CompletedTask;
            }

            return conn.SendAsync(new WireMessage { Type = "subscribe", Pattern = pattern }, _json, ct);
        }

        // If server mode, local subscriptions are enough (server routes to local via DispatchLocalAsync).
        return Task.CompletedTask;
    }

    public async Task PublishAsync(Envelope envelope, CancellationToken ct = default)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));
        if (_cts is null) throw new InvalidOperationException("Transport not started");

        // In server mode, publish means: dispatch locally + route out to subscribed clients.
        if (_options.ActAsServer)
        {
            await RouteFromServerAsync(envelope, ct).ConfigureAwait(false);
            return;
        }

        // In client mode, send to server.
        var conn = _clientConn ?? throw new InvalidOperationException("Transport not started.");
        await conn.SendAsync(new WireMessage { Type = "publish", Envelope = envelope }, _json, ct).ConfigureAwait(false);
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_cts is not null) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (_options.ActAsServer)
        {
            StartServer(_cts.Token);
            return Task.CompletedTask;
        }

        StartClient(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_cts is null) return;

        _cts.Cancel();

        // Client cleanup
        if (_clientConn is not null)
        {
            await _clientConn.DisposeAsync().ConfigureAwait(false);
            _clientConn = null;
        }

        // Server cleanup
        foreach (var kv in _clients)
            await kv.Value.DisposeAsync().ConfigureAwait(false);
        _clients.Clear();

        if (_listenSocket is not null)
        {
            try { _listenSocket.Close(); } catch { /* ignore */ }
            _listenSocket.Dispose();
            _listenSocket = null;
        }

        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.WaitAsync(ct).ConfigureAwait(false); } catch { /* ignore */ }
            _acceptLoop = null;
        }

        _cts.Dispose();
        _cts = null;

        // Server removes the socket file
        if (_options.ActAsServer && _options.DeleteSocketFileOnStop)
        {
            try
            {
                if (File.Exists(_options.SocketPath))
                    File.Delete(_options.SocketPath);
            }
            catch { /* ignore */ }
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private void StartServer(CancellationToken ct)
    {
        // Ensure old file is gone (common after crashes)
        if (File.Exists(_options.SocketPath))
        {
            try { File.Delete(_options.SocketPath); } catch { /* ignore */ }
        }

        _listenSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listenSocket.Bind(new UnixDomainSocketEndPoint(_options.SocketPath));
        _listenSocket.Listen(_options.Backlog);

        _acceptLoop = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                Socket? sock = null;
                try
                {
                    sock = await _listenSocket.AcceptAsync(ct).ConfigureAwait(false);
                    var conn = new ClientConnection(sock, _options.MaxFrameBytes);
                    _clients[conn.Id] = conn;

                    _ = Task.Run(() => ServerClientLoopAsync(conn, ct), ct);
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    sock?.Dispose();
                    // keep accepting
                }
            }
        }, ct);
    }

    private void StartClient(CancellationToken ct)
    {
        var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        sock.Connect(new UnixDomainSocketEndPoint(_options.SocketPath));

        var conn = new ClientConnection(sock, _options.MaxFrameBytes);
        _clientConn = conn;

        while (_pendingClientSubscriptions.TryDequeue(out var pattern))
        {
            conn.SendAsync(new WireMessage { Type = "subscribe", Pattern = pattern }, _json, ct)
                .GetAwaiter()
                .GetResult();
        }

        _ = Task.Run(() => ClientReceiveLoopAsync(conn, ct), ct);
    }

    private async Task ServerClientLoopAsync(ClientConnection conn, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await conn.ReceiveAsync<WireMessage>(_json, ct).ConfigureAwait(false);
                if (msg is null) break;

                switch (msg.Type)
                {
                    case "subscribe":
                        if (!string.IsNullOrWhiteSpace(msg.Pattern))
                            conn.AddSubscription(msg.Pattern);
                        break;

                    case "publish":
                        if (msg.Envelope is not null)
                            await RouteFromServerAsync(msg.Envelope, ct).ConfigureAwait(false);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { /* drop */ }
        finally
        {
            _clients.TryRemove(conn.Id, out _);
            await conn.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task ClientReceiveLoopAsync(ClientConnection conn, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await conn.ReceiveAsync<WireMessage>(_json, ct).ConfigureAwait(false);
                if (msg is null) break;

                if (msg.Type == "envelope" && msg.Envelope is not null)
                    await DispatchLocalAsync(msg.Envelope, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch { /* drop */ }
    }

    private async Task RouteFromServerAsync(Envelope envelope, CancellationToken ct)
    {
        // 1) deliver to local handlers in the server process
        await DispatchLocalAsync(envelope, ct).ConfigureAwait(false);

        // 2) route out to connected clients with matching subscriptions
        var routingKey = RoutingKey.FromEnvelope(envelope);

        var delivery = new WireMessage { Type = "envelope", Envelope = envelope };

        foreach (var kv in _clients)
        {
            var client = kv.Value;
            if (!client.MatchesAny(routingKey)) continue;

            try
            {
                await client.SendAsync(delivery, _json, ct).ConfigureAwait(false);
            }
            catch
            {
                // If a client is dead, let its loop clean it up.
            }
        }
    }

    private async Task DispatchLocalAsync(Envelope envelope, CancellationToken ct)
    {
        var routingKey = RoutingKey.FromEnvelope(envelope);

        foreach (var kv in _localHandlers)
        {
            var pattern = kv.Key;
            if (!TopicMatcher.IsMatch(pattern, routingKey)) continue;

            foreach (var handler in kv.Value)
            {
                // Fire sequentially to preserve determinism; change to parallel if you want.
                await handler(envelope, ct).ConfigureAwait(false);
            }
        }
    }

    private sealed class ClientConnection : IAsyncDisposable
    {
        private readonly Socket _socket;
        private readonly int _maxFrameBytes;
        private readonly NetworkStream _stream;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly ConcurrentBag<string> _subscriptions = new();

        public Guid Id { get; } = Guid.NewGuid();

        public ClientConnection(Socket socket, int maxFrameBytes)
        {
            _socket = socket;
            _maxFrameBytes = maxFrameBytes;
            _stream = new NetworkStream(_socket, ownsSocket: true);
        }

        public void AddSubscription(string pattern) => _subscriptions.Add(pattern);

        public bool MatchesAny(string routingKey)
        {
            foreach (var p in _subscriptions)
                if (TopicMatcher.IsMatch(p, routingKey))
                    return true;
            return false;
        }

        public async Task SendAsync<T>(T message, JsonSerializerOptions json, CancellationToken ct)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, json);
            if (bytes.Length > _maxFrameBytes)
                throw new InvalidOperationException($"Frame too large: {bytes.Length} bytes (max {_maxFrameBytes}).");

            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);

            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _stream.WriteAsync(header, ct).ConfigureAwait(false);
                await _stream.WriteAsync(bytes, ct).ConfigureAwait(false);
                await _stream.FlushAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task<T?> ReceiveAsync<T>(JsonSerializerOptions json, CancellationToken ct)
        {
            var header = await ReadExactAsync(4, ct).ConfigureAwait(false);
            if (header is null) return default;

            var len = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (len <= 0 || len > _maxFrameBytes)
                throw new InvalidOperationException($"Invalid frame length: {len}");

            var payload = await ReadExactAsync(len, ct).ConfigureAwait(false);
            if (payload is null) return default;

            return JsonSerializer.Deserialize<T>(payload, json);
        }

        private async Task<byte[]?> ReadExactAsync(int count, CancellationToken ct)
        {
            var buf = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                var read = await _stream.ReadAsync(buf.AsMemory(offset, count - offset), ct).ConfigureAwait(false);
                if (read == 0) return null;
                offset += read;
            }
            return buf;
        }

        public async ValueTask DisposeAsync()
        {
            try { _stream.Dispose(); } catch { /* ignore */ }
            _sendLock.Dispose();
            await Task.CompletedTask;
        }
    }

    private sealed class WireMessage
    {
        public string? Type { get; set; }
        public string? Pattern { get; set; }
        public Envelope? Envelope { get; set; }
    }

    private static class RoutingKey
    {
        public static string FromEnvelope(Envelope e)
        {
            // Matches README semantics:
            // Request: {service}.{verb}
            // Event: {topic}
            // Response/Error: reply.{client_id} (Meta["client_id"])
            // 
            return e.Kind switch
            {
                EnvelopeKind.Request => $"{e.Service}.{e.Verb}",
                EnvelopeKind.Event => e.Topic ?? throw new InvalidOperationException("Event requires Topic."),
                EnvelopeKind.Response or EnvelopeKind.Error => $"reply.{GetClientId(e)}",
                _ => throw new InvalidOperationException($"Unknown envelope kind: {e.Kind}")
            };
        }

        private static string GetClientId(Envelope e)
        {
            if (e.Meta is null) throw new InvalidOperationException("Meta required for reply routing.");
            if (!e.Meta.TryGetValue("client_id", out var v) || v is null)
                throw new InvalidOperationException("Meta[\"client_id\"] required for reply routing.");
            return v.ToString()!;
        }
    }

    /// <summary>
    /// Dot-delimited topic matching with AMQP-style wildcards:
    /// * matches one segment, # matches zero or more segments.
    /// </summary>
    private static class TopicMatcher
    {
        public static bool IsMatch(string pattern, string key)
        {
            var p = Split(pattern);
            var k = Split(key);
            return Match(p, 0, k, 0);
        }

        private static string[] Split(string s) =>
            s.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static bool Match(string[] p, int pi, string[] k, int ki)
        {
            // End of pattern
            if (pi == p.Length) return ki == k.Length;

            var token = p[pi];

            if (token == "#")
            {
                // # matches zero or more segments
                if (pi == p.Length - 1) return true; // trailing # matches rest
                for (var skip = ki; skip <= k.Length; skip++)
                    if (Match(p, pi + 1, k, skip))
                        return true;
                return false;
            }

            if (ki == k.Length) return false;

            if (token == "*" || string.Equals(token, k[ki], StringComparison.OrdinalIgnoreCase))
                return Match(p, pi + 1, k, ki + 1);

            return false;
        }
    }
}

public sealed class UnixSocketTransportOptions
{
    /// <summary>Filesystem path for the Unix domain socket.</summary>
    public required string SocketPath { get; init; }

    /// <summary>If true, this transport hosts the socket and routes between clients.</summary>
    public bool ActAsServer { get; init; } = true;

    /// <summary>Listen backlog (server mode).</summary>
    public int Backlog { get; init; } = 64;

    /// <summary>Maximum allowed frame size in bytes.</summary>
    public int MaxFrameBytes { get; init; } = 1024 * 1024; // 1 MiB default

    /// <summary>Delete socket file on StopAsync (server mode).</summary>
    public bool DeleteSocketFileOnStop { get; init; } = true;
}
