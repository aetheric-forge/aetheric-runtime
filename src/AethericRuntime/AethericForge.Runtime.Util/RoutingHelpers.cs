using AethericForge.Runtime.Bus.Abstractions;

namespace AethericForge.Runtime.Util;

public class RoutingHelpers
{
    
    public static string ResolveRoutingKey(Envelope e) =>
        e.Kind switch
        {
            "request" => $"{e.Service}.{e.Verb}",
            "response" or "error" => $"reply.{e.Meta?["client_id"]}",
            "event" => e.Topic!,
            _ => throw new InvalidOperationException("Unknown envelope kind.")
        }; 
}