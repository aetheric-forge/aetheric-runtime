using System.Collections.Immutable;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Keycloak;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class KeycloakResourceProviderTests
{
    private static ResourceBinding Binding => new("keycloak",
        new Dictionary<string, string> { ["realm"] = "test-realm" }.ToImmutableDictionary(),
        ImmutableDictionary<string, SecretReference>.Empty);
    private static ResourceRequirement Resource => new("registry", "Registry", "identity", "owned");

    // Validate is pure - no connection is ever attempted - so a dummy credential is fine
    // regardless of whether a live server is available for the integration tests below.
    private static RootCredential DummyCredential => new("localhost", 8080, "unused", "unused");

    [Theory]
    [InlineData("realm", "")]
    [InlineData("realm", "has a space")]
    [InlineData("realm", "semicolon;here")]
    [InlineData("extra", "value")]
    public void Invalid_bindings_are_rejected(string key, string value)
    {
        using var provider = new KeycloakResourceProvider(DummyCredential);
        Assert.NotEmpty(provider.Validate(Resource, Binding with { Settings = Binding.Settings.SetItem(key, value) }));
    }

    [Fact]
    public void Bindings_with_secrets_are_rejected()
    {
        using var provider = new KeycloakResourceProvider(DummyCredential);
        var binding = Binding with { Secrets = ImmutableDictionary<string, SecretReference>.Empty.Add("realm", new("leaked")) };
        Assert.NotEmpty(provider.Validate(Resource, binding));
    }

    [Fact]
    public void Parent_owned_resources_are_rejected()
    {
        using var provider = new KeycloakResourceProvider(DummyCredential);
        Assert.NotEmpty(provider.Validate(Resource with { Ownership = "parent" }, Binding));
    }

    // scopedClientId is derived as "{institutionId}-{resourceId}" - a random institutionId per
    // test (not just a random realm name) keeps each run's client identity unique, so a prior
    // run's leftover client can never make a "new client" run observe AlreadyExists: true.
    [KeycloakFact]
    public async Task New_realm_creates_a_scoped_service_account_client()
    {
        using var provider = new KeycloakResourceProvider(KeycloakFactAttribute.RootCredential());
        var institutionId = "test-" + Guid.NewGuid().ToString("N");
        var realm = "test-" + Guid.NewGuid().ToString("N")[..16];
        var binding = Binding with { Settings = Binding.Settings.SetItem("realm", realm) };
        var context = new ProviderContext("plan", "development", institutionId, Resource, binding, new InMemorySecrets());

        var result = await provider.EnsureAsync(context, default);

        Assert.False(result.AlreadyExists);
        Assert.Single(result.Secrets);
        var scopedClientId = $"{institutionId}-registry";
        var roles = await AssignedRealmManagementRolesAsync(realm, scopedClientId);
        Assert.Contains("manage-users", roles);
        Assert.Contains("view-users", roles);
    }

    [KeycloakFact]
    public async Task Existing_client_is_updated_not_recreated_and_reuses_the_same_secret()
    {
        using var provider = new KeycloakResourceProvider(KeycloakFactAttribute.RootCredential());
        var institutionId = "test-" + Guid.NewGuid().ToString("N");
        var realm = "test-" + Guid.NewGuid().ToString("N")[..16];
        var binding = Binding with { Settings = Binding.Settings.SetItem("realm", realm) };
        var secrets = new InMemorySecrets();
        var context = new ProviderContext("plan", "development", institutionId, Resource, binding, secrets);

        var first = await provider.EnsureAsync(context, default);
        var second = await provider.EnsureAsync(context, default);

        Assert.False(first.AlreadyExists);
        Assert.True(second.AlreadyExists);
        Assert.Equal(first.Secrets.Single(), second.Secrets.Single());
        Assert.Equal(1, secrets.CreatedCount);
    }

    private static async Task<HashSet<string>> AssignedRealmManagementRolesAsync(string realm, string clientId)
    {
        using var http = new HttpClient { BaseAddress = new Uri(KeycloakFactAttribute.Origin()) };
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["client_id"] = "admin-cli",
            ["username"] = KeycloakFactAttribute.AdminUsername(), ["password"] = KeycloakFactAttribute.AdminPassword(),
        });
        using var tokenResponse = await http.PostAsync("realms/master/protocol/openid-connect/token", form);
        tokenResponse.EnsureSuccessStatusCode();
        using var tokenJson = await JsonDocument.ParseAsync(await tokenResponse.Content.ReadAsStreamAsync());
        var token = tokenJson.RootElement.GetProperty("access_token").GetString();

        HttpRequestMessage Authorized(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        using var clientsResponse = await http.SendAsync(Authorized(HttpMethod.Get,
            $"admin/realms/{Uri.EscapeDataString(realm)}/clients?clientId={Uri.EscapeDataString(clientId)}&exact=true"));
        clientsResponse.EnsureSuccessStatusCode();
        using var clientsJson = await JsonDocument.ParseAsync(await clientsResponse.Content.ReadAsStreamAsync());
        var internalId = clientsJson.RootElement.EnumerateArray().Single().GetProperty("id").GetString()!;

        using var accountResponse = await http.SendAsync(Authorized(HttpMethod.Get,
            $"admin/realms/{Uri.EscapeDataString(realm)}/clients/{Uri.EscapeDataString(internalId)}/service-account-user"));
        accountResponse.EnsureSuccessStatusCode();
        using var accountJson = await JsonDocument.ParseAsync(await accountResponse.Content.ReadAsStreamAsync());
        var userId = accountJson.RootElement.GetProperty("id").GetString()!;

        using var realmMgmtResponse = await http.SendAsync(Authorized(HttpMethod.Get,
            $"admin/realms/{Uri.EscapeDataString(realm)}/clients?clientId=realm-management&exact=true"));
        realmMgmtResponse.EnsureSuccessStatusCode();
        using var realmMgmtJson = await JsonDocument.ParseAsync(await realmMgmtResponse.Content.ReadAsStreamAsync());
        var realmMgmtId = realmMgmtJson.RootElement.EnumerateArray().Single().GetProperty("id").GetString()!;

        using var rolesResponse = await http.SendAsync(Authorized(HttpMethod.Get,
            $"admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(userId)}/role-mappings/clients/{Uri.EscapeDataString(realmMgmtId)}"));
        rolesResponse.EnsureSuccessStatusCode();
        using var rolesJson = await JsonDocument.ParseAsync(await rolesResponse.Content.ReadAsStreamAsync());
        return rolesJson.RootElement.EnumerateArray().Select(r => r.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal);
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

public sealed class KeycloakFactAttribute : FactAttribute
{
    // Format: http://admin:admin@127.0.0.1:8080 - userinfo carries the root admin username/password.
    public static bool HasCredential => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PROVISIONING_TEST_KEYCLOAK"));

    public KeycloakFactAttribute()
    {
        if (!HasCredential) Skip = "Set PROVISIONING_TEST_KEYCLOAK to a http://user:pass@host:port root admin connection for an isolated server.";
    }

    private static Uri ParsedUri() => new(Environment.GetEnvironmentVariable("PROVISIONING_TEST_KEYCLOAK")!);
    public static string Origin() => ParsedUri().GetLeftPart(UriPartial.Authority) + "/";
    public static string AdminUsername() => Uri.UnescapeDataString(ParsedUri().UserInfo.Split(':')[0]);
    public static string AdminPassword() => Uri.UnescapeDataString(ParsedUri().UserInfo.Split(':')[1]);

    public static RootCredential RootCredential()
    {
        var uri = ParsedUri();
        return new RootCredential(uri.Host, uri.Port, AdminUsername(), AdminPassword())
        { Keycloak = new KeycloakRootOptions(uri.Scheme) };
    }
}
