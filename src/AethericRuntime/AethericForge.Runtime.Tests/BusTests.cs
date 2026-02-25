namespace AethericForge.Runtime.Tests;

using AethericForge.Runtime.Bus.Abstractions;

public class BusTests
{
    public static IEnumerable<object[]> Cases => TestMatrix.BusCases();

    private static Envelope RequestEnvelope(string service, string verb, object payload) =>
        new()
        {
            Kind = "request",
            Service = service,
            Verb = verb,
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(payload)
        };

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
    public async Task Publish_To_Exact_Route_Invokes_Handler(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();

        int count = 0;
        broker.Route("alpha.beta", (_, _) => { count++; return Task.CompletedTask; });

        var message = new TestModels.TestMessage("alpha.beta");
        var envelope = RequestEnvelope("alpha", "beta", message);

        await transport.StartAsync();
        await broker.PublishAsync(envelope);

        await Eventually(() => count == 1);
        await transport.StopAsync();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Publish_With_Wildcards_Dispatches_Correctly(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        int star = 0, hash = 0, exact = 0;

        broker.Route("alpha.*.gamma", (_, _) => { star++; return Task.CompletedTask; });
        broker.Route("alpha.#", (_, _) => { hash++; return Task.CompletedTask; });
        broker.Route("alpha.beta.gamma", (_, _) => { exact++; return Task.CompletedTask; });

        var message = new TestModels.TestMessage("alpha.beta.gamma");
        var envelope = RequestEnvelope("alpha.beta", "gamma", message);

        await transport.StartAsync();
        await broker.PublishAsync(envelope);

        await Eventually(() => exact == 1);
        await Eventually(() => star == 1);
        await Eventually(() => hash == 1);
        await transport.StopAsync();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Multiple_Handlers_All_Invoked(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        int a = 0, b = 0;
        broker.Route("x.y", (_, _) => { a++; return Task.CompletedTask; });
        broker.Route("x.y", (_, _) => { b++; return Task.CompletedTask; });

        await transport.StartAsync();

        var message = new TestModels.TestMessage("x.y");
        var envelope = RequestEnvelope("x", "y", message);
        await broker.PublishAsync(envelope);

        await Eventually(() => a == 1);
        await Eventually(() => b == 1);
        await transport.StopAsync();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Publish_Before_Start_Throws(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        var message = new TestModels.TestMessage("a.b");
        var envelope = RequestEnvelope("a", "b", message);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await broker.PublishAsync(envelope);
        });
    }
    
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Publish_With_No_Matching_Route_Does_Not_Throw(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        await transport.StartAsync();

        var message = new TestModels.TestMessage("no.handlers.here");
        var envelope = RequestEnvelope("no.handlers", "here", message);

        var ex = await Record.ExceptionAsync(() =>
            broker.PublishAsync(envelope));
        Assert.Null(ex);
        await transport.StopAsync();
    }
    
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Star_Wildcard_Does_Not_Match_Extra_Segments(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        int count = 0;

        broker.Route("alpha.*", (_, _) => { count++; return Task.CompletedTask; });

        var message = new TestModels.TestMessage("alpha.beta.gamma");
        var envelope = RequestEnvelope("alpha.beta", "gamma", message);

        await transport.StartAsync();
        await broker.PublishAsync(envelope);

        Assert.Equal(0, count);
        await transport.StopAsync();
    }
    
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Hash_Wildcard_As_Single_Segment_Matches_All(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        int count = 0;

        broker.Route("#", (_, _) => { count++; return Task.CompletedTask; });

        await transport.StartAsync();

        var message = new TestModels.TestMessage("alpha.beta.gamma");
        var envelope = RequestEnvelope("alpha.beta", "gamma", message);

        await broker.PublishAsync(envelope);

        await Eventually(() => count == 1);
        await transport.StopAsync();
    }
    
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Route_After_Start_Still_Receives_Messages(Func<(ITransport transport, IBroker broker)> factory)
    {
        var (transport, broker) = factory();
        int count = 0;

        await transport.StartAsync();
        broker.Route("alpha.beta", (_, _) => { count++; return Task.CompletedTask; });
        var message = new TestModels.TestMessage("alpha.beta");
        var envelope = RequestEnvelope("alpha", "beta", message);

        await broker.PublishAsync(envelope);

        await Eventually(() => count == 1);
        await transport.StopAsync();
    }
}
