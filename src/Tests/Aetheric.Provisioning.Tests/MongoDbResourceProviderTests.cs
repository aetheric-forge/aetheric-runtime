using System.Collections.Immutable;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.MongoDb;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class MongoDbResourceProviderTests
{
    private static ResourceBinding Binding => new("mongodb",
        new Dictionary<string, string> { ["database"] = "test-library" }.ToImmutableDictionary(),
        ImmutableDictionary<string, SecretReference>.Empty);
    private static ResourceRequirement Resource => new("library", "Library", "knowledge", "owned");

    // Validate is pure - no connection is ever attempted - so a dummy credential is fine
    // regardless of whether a live broker is available for the integration tests below.
    private static RootCredential DummyCredential => new("localhost", 27017, "unused", "unused");

    [Theory]
    [InlineData("database", "")]
    [InlineData("database", "has a space")]
    [InlineData("database", "semicolon;here")]
    [InlineData("extra", "value")]
    public void Invalid_bindings_are_rejected(string key, string value)
    {
        using var provider = new MongoDbResourceProvider(DummyCredential);
        Assert.NotEmpty(provider.Validate(Resource, Binding with { Settings = Binding.Settings.SetItem(key, value) }));
    }

    [Fact]
    public void Bindings_with_secrets_are_rejected()
    {
        using var provider = new MongoDbResourceProvider(DummyCredential);
        var binding = Binding with { Secrets = ImmutableDictionary<string, SecretReference>.Empty.Add("database", new("leaked")) };
        Assert.NotEmpty(provider.Validate(Resource, binding));
    }

    [Fact]
    public void Parent_owned_resources_are_rejected()
    {
        using var provider = new MongoDbResourceProvider(DummyCredential);
        Assert.NotEmpty(provider.Validate(Resource with { Ownership = "parent" }, Binding));
    }

    // scopedUser is derived as "{institutionId}-{resourceId}" - a random institutionId per test
    // (not just a random database name) keeps each run's Mongo user identity unique, so a prior
    // run's leftover admin-database user can never make a "new user" run observe AlreadyExists: true.
    [MongoDbFact]
    public async Task New_database_creates_a_scoped_user_with_readWrite()
    {
        using var provider = new MongoDbResourceProvider(MongoDbFactAttribute.RootCredential());
        var institutionId = "test-" + Guid.NewGuid().ToString("N");
        var database = "test-" + Guid.NewGuid().ToString("N");
        var binding = Binding with { Settings = Binding.Settings.SetItem("database", database) };
        var context = new ProviderContext("plan", "development", institutionId, Resource, binding, new InMemorySecrets());

        var result = await provider.EnsureAsync(context, default);

        Assert.False(result.AlreadyExists);
        Assert.Single(result.Secrets);
        var scopedUser = $"{institutionId}-library";
        var roles = await RolesForUserAsync(scopedUser);
        Assert.Contains(roles, r => r["role"].AsString == "readWrite" && r["db"].AsString == database);
    }

    [MongoDbFact]
    public async Task Existing_user_is_updated_not_recreated_and_reuses_the_same_secret()
    {
        using var provider = new MongoDbResourceProvider(MongoDbFactAttribute.RootCredential());
        var institutionId = "test-" + Guid.NewGuid().ToString("N");
        var database = "test-" + Guid.NewGuid().ToString("N");
        var binding = Binding with { Settings = Binding.Settings.SetItem("database", database) };
        var secrets = new InMemorySecrets();
        var context = new ProviderContext("plan", "development", institutionId, Resource, binding, secrets);

        var first = await provider.EnsureAsync(context, default);
        var second = await provider.EnsureAsync(context, default);

        Assert.False(first.AlreadyExists);
        Assert.True(second.AlreadyExists);
        Assert.Equal(first.Secrets.Single(), second.Secrets.Single());
        Assert.Equal(1, secrets.CreatedCount);
    }

    private static async Task<BsonArray> RolesForUserAsync(string username)
    {
        var client = new MongoClient(MongoDbFactAttribute.ConnectionString());
        var admin = client.GetDatabase("admin");
        var command = new BsonDocument { { "usersInfo", new BsonDocument { { "user", username }, { "db", "admin" } } } };
        var result = await admin.RunCommandAsync<BsonDocument>(command);
        return result["users"].AsBsonArray[0]["roles"].AsBsonArray;
    }

    private sealed class InMemorySecrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
        public int CreatedCount { get; private set; }

        public Task<SecretReference> GetOrCreateAsync(string scope, string name, CancellationToken ct)
        {
            var id = scope + ":" + name;
            if (!_values.ContainsKey(id))
            {
                _values[id] = Guid.NewGuid().ToString("N");
                CreatedCount++;
            }
            return Task.FromResult(new SecretReference(id));
        }

        public Task<string> ReadAsync(SecretReference reference, CancellationToken ct) => Task.FromResult(_values[reference.Id]);
    }
}

public sealed class MongoDbFactAttribute : FactAttribute
{
    public static bool HasCredential => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PROVISIONING_TEST_MONGO"));

    public MongoDbFactAttribute()
    {
        if (!HasCredential) Skip = "Set PROVISIONING_TEST_MONGO to a mongodb:// root connection string for an isolated broker.";
    }

    public static string ConnectionString() => Environment.GetEnvironmentVariable("PROVISIONING_TEST_MONGO")!;

    public static RootCredential RootCredential()
    {
        var uri = new MongoUrl(ConnectionString());
        return new RootCredential(uri.Server!.Host, uri.Server.Port, uri.Username, uri.Password!)
        { Mongo = new MongoRootOptions(uri.AuthenticationSource ?? "admin") };
    }
}
