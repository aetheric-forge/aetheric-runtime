using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Model.Messages;

namespace AethericForge.Runtime.Tests;

public class BusTests
{
    public static IEnumerable<object[]> Cases => TestMatrix.BusCases();

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

    private sealed class TestMessage(string? type = null) : Message(id: null, type: type)
    {
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Publish_To_Exact_Route_Invokes_Handler(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();

        int count = 0;
        broker.Route("alpha.beta", _ => { count++; return Task.CompletedTask; });

        await transport.Start();
        await broker.Publish(new TestMessage(type: "alpha.beta"));

        await Eventually(() => count == 1);
        await transport.Stop();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Publish_With_Wildcards_Dispatches_Correctly(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        int star = 0, hash = 0, exact = 0;

        broker.Route("alpha.*.gamma", _ => { star++; return Task.CompletedTask; });
        broker.Route("alpha.#", _ => { hash++; return Task.CompletedTask; });
        broker.Route("alpha.beta.gamma", _ => { exact++; return Task.CompletedTask; });

        await transport.Start();
        await broker.Publish(new TestMessage(type: "alpha.beta.gamma"));

        await Eventually(() => exact == 1);
        await Eventually(() => star == 1);
        await Eventually(() => hash == 1);
        await transport.Stop();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Multiple_Handlers_All_Invoked(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        int a = 0, b = 0;
        broker.Route("x.y", _ => { a++; return Task.CompletedTask; });
        broker.Route("x.y", _ => { b++; return Task.CompletedTask; });

        await transport.Start();
        await broker.Publish(new TestMessage(type: "x.y"));

        await Eventually(() => a == 1);
        await Eventually(() => b == 1);
        await transport.Stop();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Publish_Before_Start_Throws(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await broker.Publish(new TestMessage(type: "a.b"));
        });
    }
    
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Publish_With_No_Matching_Route_Does_Not_Throw(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        await transport.Start();
        var ex = await Record.ExceptionAsync(() =>
            broker.Publish(new TestMessage(type: "no.handlers.here")));
        Assert.Null(ex);
        await transport.Stop();
    }
    
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Star_Wildcard_Does_Not_Match_Extra_Segments(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        int count = 0;

        broker.Route("alpha.*", _ => { count++; return Task.CompletedTask; });

        await transport.Start();
        await broker.Publish(new TestMessage(type: "alpha.beta.gamma"));

        Assert.Equal(0, count);
        await transport.Stop();
    }
    
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Hash_Wildcard_As_Single_Segment_Matches_All(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        int count = 0;

        broker.Route("#", _ => { count++; return Task.CompletedTask; });

        await transport.Start();
        await broker.Publish(new TestMessage(type: "alpha.beta.gamma"));

        await Eventually(() => count == 1);
        await transport.Stop();
    }
    
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Route_After_Start_Still_Receives_Messages(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        int count = 0;

        await transport.Start();
        broker.Route("alpha.beta", _ => { count++; return Task.CompletedTask; });

        await broker.Publish(new TestMessage(type: "alpha.beta"));

        await Eventually(() => count == 1);
        await transport.Stop();
    }
}
