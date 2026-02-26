using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Hosting;

namespace AethericForge.Runtime.Tests;

public class HostTests
{
    public static IEnumerable<object[]> Cases => TestMatrix.HostTransportCases();

    private static async Task Eventually(Func<bool> condition, TimeSpan? timeout = null, TimeSpan? poll = null)
    {
        var t = timeout ?? TimeSpan.FromSeconds(2);
        var p = poll ?? TimeSpan.FromMilliseconds(25);
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < t)
        {
            if (condition()) return;
            await Task.Delay(p);
        }

        Assert.True(condition());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Build_Start_Stop_With_Envelope_Route_Works(Func<ITransport> transportFactory)
    {
        var counter = new Counter();
        var transport = transportFactory();
        var host = await AethericHost
            .Create("svc")
            .UseTransport(transport)
            .AddEnvelopeHandler("alpha.beta", (_, _) =>
            {
                Interlocked.Increment(ref counter.Value);
                return Task.CompletedTask;
            })
            .BuildAsync();

        try
        {
            await host.StartAsync();
            await host.Broker.PublishAsync(new Envelope<TestModels.TestMessage>(
                "alpha.beta",
                new TestModels.TestMessage("alpha.beta")));

            await Eventually(() => Volatile.Read(ref counter.Value) == 1);
        }
        finally
        {
            await host.StopAsync();
            await host.DisposeAsync();
        }
    }

    private sealed class Counter
    {
        public int Value;
    }
}
