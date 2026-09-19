using System.Collections.Immutable;
using System.Net;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.RabbitMq;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class RabbitMqResourceProviderTests
{
    private static ResourceBinding Binding => new("rabbitmq",
        new Dictionary<string, string> { ["vhost"] = "test-campus" }.ToImmutableDictionary(),
        ImmutableDictionary<string, SecretReference>.Empty);
    private static ResourceRequirement Resource => new("post-office", "Post Office", "post", "owned");
    private static RootCredential RootCredential => new("rabbit.local", 15672, "root", "root-password")
    { RabbitMq = new RabbitMqRootOptions() };

    [Theory]
    [InlineData("vhost", "")]
    [InlineData("vhost", "has a space")]
    [InlineData("vhost", "semicolon;here")]
    [InlineData("extra", "value")]
    public void Invalid_bindings_are_rejected(string key, string value)
    {
        using var provider = new RabbitMqResourceProvider(RootCredential, new FakeHandler());
        Assert.NotEmpty(provider.Validate(Resource, Binding with { Settings = Binding.Settings.SetItem(key, value) }));
    }

    [Fact]
    public void Bindings_with_secrets_are_rejected()
    {
        using var provider = new RabbitMqResourceProvider(RootCredential, new FakeHandler());
        var binding = Binding with { Secrets = ImmutableDictionary<string, SecretReference>.Empty.Add("vhost", new("leaked")) };
        Assert.NotEmpty(provider.Validate(Resource, binding));
    }

    [Fact]
    public void Parent_owned_resources_are_rejected()
    {
        using var provider = new RabbitMqResourceProvider(RootCredential, new FakeHandler());
        Assert.NotEmpty(provider.Validate(Resource with { Ownership = "parent" }, Binding));
    }

    [Fact]
    public async Task New_vhost_creates_vhost_user_and_permissions()
    {
        var handler = new FakeHandler();
        using var provider = new RabbitMqResourceProvider(RootCredential, handler);
        var context = new ProviderContext("plan", "development", "campus", Resource, Binding, new InMemorySecrets());

        var result = await provider.EnsureAsync(context, default);

        Assert.False(result.AlreadyExists);
        Assert.Single(result.Secrets);
        Assert.Equal(new[]
        {
            "GET api/vhosts/test-campus",
            "PUT api/vhosts/test-campus",
            "PUT api/users/campus-post-office",
            "PUT api/permissions/test-campus/campus-post-office",
        }, handler.Requests);
    }

    [Fact]
    public async Task Existing_vhost_skips_creation_but_still_ensures_user_and_permissions()
    {
        var handler = new FakeHandler { VhostExists = true };
        using var provider = new RabbitMqResourceProvider(RootCredential, handler);
        var context = new ProviderContext("plan", "development", "campus", Resource, Binding, new InMemorySecrets());

        var result = await provider.EnsureAsync(context, default);

        Assert.True(result.AlreadyExists);
        Assert.Equal(new[]
        {
            "GET api/vhosts/test-campus",
            "PUT api/users/campus-post-office",
            "PUT api/permissions/test-campus/campus-post-office",
        }, handler.Requests);
    }

    [Fact]
    public async Task Reuses_the_same_secret_across_calls_instead_of_rotating_it()
    {
        var handler = new FakeHandler();
        using var provider = new RabbitMqResourceProvider(RootCredential, handler);
        var secrets = new InMemorySecrets();
        var context = new ProviderContext("plan", "development", "campus", Resource, Binding, secrets);

        var first = await provider.EnsureAsync(context, default);
        var second = await provider.EnsureAsync(context, default);

        Assert.Equal(first.Secrets.Single(), second.Secrets.Single());
        Assert.Equal(1, secrets.CreatedCount);
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

    private sealed class FakeHandler : HttpMessageHandler
    {
        public bool VhostExists { get; init; }
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath.TrimStart('/')}");
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);

            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.StartsWith("/api/vhosts/", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(VhostExists ? HttpStatusCode.OK : HttpStatusCode.NotFound));

            if (request.Method == HttpMethod.Put)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotImplemented));
        }
    }
}
