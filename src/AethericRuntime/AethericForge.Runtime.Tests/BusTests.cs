using AethericForge.Runtime.Bus.Abstractions;
using Xunit;

namespace AethericForge.Runtime.Tests;

public class BusTests
{
    public static IEnumerable<object[]> Cases => TestMatrix.BusCases();

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Request_Envelope_Routed_To_Handler_By_RouteKey(Func<IBroker> factory)
    {
        var broker = factory();
        var message = new TestModels.TestMessage("Envelope_Routed_To_Handler_By_RouteKey");
        var routeKey = new RouteKey(kind: EnvelopeKind.Request, service: "BusTests", verb: "Envelope_Routed_To_Handler_By_RouteKey");
        var envelope = new Envelope<TestModels.TestMessage>
            (
                kind: EnvelopeKind.Request,
                routeKey: routeKey,
                payload: message,
                meta: new(),
                id: Guid.NewGuid(),
                correlationId: Guid.NewGuid()
            );

        var count = 0;
        var messageHandled = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        await broker.Transport.SubscribeAsync(routeKey, (_, _) =>
        {
            count++;
            messageHandled.TrySetResult(null);
            return Task.CompletedTask;
        });

        await broker.Transport.StartAsync();
        await broker.PublishAsync(envelope);
        await messageHandled.Task;

        Assert.Equal(1, count);
    }
}

