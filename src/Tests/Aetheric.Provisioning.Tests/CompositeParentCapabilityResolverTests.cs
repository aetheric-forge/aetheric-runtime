using Aetheric.Provisioning.Engine;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class CompositeParentCapabilityResolverTests
{
    private static ParentContext Parent => new("https://example.test/fixture", "0000000000000000000000000000000000000000");

    [Fact]
    public async Task Each_sub_resolver_answers_only_its_own_contract()
    {
        using var composite = new CompositeParentCapabilityResolver([new FakeResolver("IArchive", true), new FakeResolver("ILibrary", true)]);
        Assert.True(await composite.IsAvailableAsync(Parent, "IArchive", "x", default));
        Assert.True(await composite.IsAvailableAsync(Parent, "ILibrary", "x", default));
    }

    [Fact]
    public async Task A_contract_none_of_the_sub_resolvers_recognize_is_unavailable_not_a_crash()
    {
        using var composite = new CompositeParentCapabilityResolver([new FakeResolver("IArchive", true)]);
        Assert.False(await composite.IsAvailableAsync(Parent, "IPostOffice", "x", default));
    }

    [Fact]
    public async Task A_genuinely_unavailable_contract_is_reported_unavailable_not_masked_by_others()
    {
        using var composite = new CompositeParentCapabilityResolver([new FakeResolver("IArchive", false), new FakeResolver("ILibrary", true)]);
        Assert.False(await composite.IsAvailableAsync(Parent, "IArchive", "x", default));
    }

    [Fact]
    public async Task Order_does_not_affect_the_result()
    {
        using var forward = new CompositeParentCapabilityResolver([new FakeResolver("IArchive", true), new FakeResolver("ILibrary", false)]);
        using var backward = new CompositeParentCapabilityResolver([new FakeResolver("ILibrary", false), new FakeResolver("IArchive", true)]);
        Assert.True(await forward.IsAvailableAsync(Parent, "IArchive", "x", default));
        Assert.True(await backward.IsAvailableAsync(Parent, "IArchive", "x", default));
    }

    [Fact]
    public async Task Disposing_the_composite_disposes_every_disposable_sub_resolver()
    {
        var a = new FakeResolver("IArchive", true);
        var b = new FakeResolver("ILibrary", true);
        (new CompositeParentCapabilityResolver([a, b]) as IDisposable)!.Dispose();
        Assert.True(a.Disposed);
        Assert.True(b.Disposed);
        await Task.CompletedTask;
    }

    private sealed class FakeResolver(string expectedContract, bool available) : IParentCapabilityResolver, IDisposable
    {
        public bool Disposed { get; private set; }

        public Task<bool> IsAvailableAsync(ParentContext parent, string contract, string source, CancellationToken cancellationToken) =>
            Task.FromResult(contract == expectedContract && available);

        public void Dispose() => Disposed = true;
    }
}
