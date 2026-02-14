using System.Collections.Concurrent;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Model.Messages;

namespace AethericForge.Runtime.Bus.Transports;

/// <summary>
/// A simple in-memory transport implementing topic-style routing with '.' separators.
/// Supports '*' to match a single segment and '#' to match zero or more segments.
/// </summary>
public class InMemoryTransport : ITransport
{
    private readonly ConcurrentDictionary<string, List<MessageHandler>> _routes = new();
    private volatile bool _started;

    public Task Start()
    {
        _started = true;
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        _started = false;
        return Task.CompletedTask;
    }

    public Task Subscribe(string pattern, MessageHandler handler)
    {
        var list = _routes.GetOrAdd(pattern, _ => new List<MessageHandler>());
        lock (list)
        {
            list.Add(handler);
        }
        return Task.CompletedTask;
    }

    public Task Publish(Message msg)
    {
        if (!_started)
            throw new InvalidOperationException("Transport not started");

        var handlers = CollectHandlers(msg.Type);
        if (handlers.Count == 0) return Task.CompletedTask;

        // invoke sequentially to keep deterministic behavior for tests
        return InvokeAll(handlers, msg);
    }

    private static async Task InvokeAll(List<MessageHandler> handlers, Message msg)
    {
        foreach (var h in handlers)
        {
            await h(msg);
        }
    }

    private List<MessageHandler> CollectHandlers(string routingKey)
    {
        var result = new List<MessageHandler>();
        foreach (var kv in _routes)
        {
            var list = kv.Value;
            bool match = TopicMatch(kv.Key, routingKey);
            if (!match) continue;
            lock (list)
            {
                result.AddRange(list);
            }
        }
        return result;
    }

    // Topic-style matcher with '.'-separated segments.
    // '*' matches a single segment, '#' matches zero or more segments.
    private static bool TopicMatch(string pattern, string key)
    {
        if (pattern == "#") return true;
        var p = pattern.Split('.');
        var k = key.Split('.');

        int i = 0, j = 0;
        while (i < p.Length && j < k.Length)
        {
            var ps = p[i];
            if (ps == "#")
            {
                // consume rest of key
                return true;
            }
            if (ps == "*" || ps == k[j])
            {
                i++; j++;
                continue;
            }
            return false;
        }

        // if key remains but pattern ended (without '#') → no match
        if (j < k.Length && (i >= p.Length || p[^1] != "#")) return false;

        // allow trailing '#' in pattern
        if (i < p.Length)
        {
            if (p[i] == "#" && i == p.Length - 1) return true;
        }

        return i == p.Length && j == k.Length;
    }
}
