using System.Collections.Immutable;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.MongoDb;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class MongoDbLibraryParentCapabilityResolverTests
{
    private static ParentContext Parent => new("https://example.test/fixture", "0000000000000000000000000000000000000000");

    [MongoDbFact]
    public async Task Unrecognized_contracts_are_always_unavailable()
    {
        var institutionId = await CreateScopedUserAsync();
        using var resolver = new MongoDbLibraryParentCapabilityResolver(MongoDbFactAttribute.RootCredential(), institutionId);
        Assert.False(await resolver.IsAvailableAsync(Parent, "ISomethingElse", "campus.library", default));
    }

    [MongoDbFact]
    public async Task An_existing_scoped_user_is_available()
    {
        var institutionId = await CreateScopedUserAsync();
        using var resolver = new MongoDbLibraryParentCapabilityResolver(MongoDbFactAttribute.RootCredential(), institutionId);
        Assert.True(await resolver.IsAvailableAsync(Parent, "ILibrary", "campus.library", default));
    }

    [MongoDbFact]
    public async Task A_scoped_user_that_was_never_created_is_unavailable()
    {
        using var resolver = new MongoDbLibraryParentCapabilityResolver(MongoDbFactAttribute.RootCredential(), "nonexistent-" + Guid.NewGuid().ToString("N"));
        Assert.False(await resolver.IsAvailableAsync(Parent, "ILibrary", "campus.library", default));
    }

    private static async Task<string> CreateScopedUserAsync()
    {
        var institutionId = "test-" + Guid.NewGuid().ToString("N")[..16];
        using var provider = new MongoDbResourceProvider(MongoDbFactAttribute.RootCredential());
        var resource = new ResourceRequirement("library", "Library", "knowledge", "owned");
        var binding = new ResourceBinding("mongodb",
            new Dictionary<string, string> { ["database"] = "test-" + Guid.NewGuid().ToString("N")[..16] }.ToImmutableDictionary(),
            ImmutableDictionary<string, SecretReference>.Empty);
        var context = new ProviderContext("plan", "development", institutionId, resource, binding, new InMemorySecrets());
        await provider.EnsureAsync(context, default);
        return institutionId;
    }

    private sealed class InMemorySecrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<SecretReference> GetOrCreateAsync(string scope, string name, CancellationToken ct)
        {
            var id = scope + ":" + name;
            if (!_values.ContainsKey(id)) _values[id] = Guid.NewGuid().ToString("N");
            return Task.FromResult(new SecretReference(id));
        }

        public Task<string> ReadAsync(SecretReference reference, CancellationToken ct) => Task.FromResult(_values[reference.Id]);
    }
}
