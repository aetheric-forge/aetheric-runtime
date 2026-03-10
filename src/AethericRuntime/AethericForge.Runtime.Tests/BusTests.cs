using AethericForge.Runtime.Bus.Abstractions;

namespace AethericForge.Runtime.Tests;

public class BusTests
{
    public static IEnumerable<object> Cases => TestMatrix.BusCases();

    public async Task Request_Envelope_Routed_To_Handler_By_RouteKey(Func<IBroker> factory)
    {
        var broker = factory();
        var message = new TestModels.TestMessage("Envelope_Routed_To_Handler_By_RouteKey");
        var routeKey = new RouteKey(service: "BusTests", verb: "Envelope_Routed_To_Handler_By_RouteKey");
        var envelope = new Envelope<TestModels.TestMessage>
            (
                kind: EnvelopeKind.Request,
                routeKey: new RouteKey(service: "BusTests", verb: "Envelope_Routed_To_Handler_By_RouteKey"),
                payload: message,
                meta: new(),
                id: Guid.NewGuid(),
                correlationId: Guid.NewGuid()
            );

        var count = 0;
        await broker.Transport.SubscribeAsync(routeKey, (_, _) => { count++; return Task.CompletedTask; });
        await broker.Transport.StartAsync();
        await broker.PublishAsync(envelope);
        Assert.Equal(count, 1);
    }
}

