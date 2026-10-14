using System.Net;
using System.Text.Json;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Keycloak;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class BrowserConsumerClientTests
{
    [Fact]
    public async Task Directory_client_can_query_membership_without_browser_login_or_management_roles()
    {
        var handler = new DirectoryClientHandler();
        using var provider = new KeycloakResourceProvider(new RootCredential("identity.example", 443, "root", "root-secret"), handler);
        await provider.EnsureBrowserClientAsync("members", "decisions-directory", new string('a', 64), "directory-secret", new Uri("https://forge.example/"), directoryReader: true);
        Assert.True(handler.Client!.Value.GetProperty("serviceAccountsEnabled").GetBoolean());
        Assert.False(handler.Client.Value.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.Equal(new[] { "query-groups", "query-users", "view-users" }, handler.Roles.Order());
        Assert.DoesNotContain("manage-users", handler.Roles);
    }

    [Fact]
    public async Task Browser_client_is_idempotent_and_has_exact_callbacks_without_management_roles()
    {
        var handler = new BrowserClientHandler();
        using var provider = new KeycloakResourceProvider(new RootCredential("identity.example", 443, "root", "root-secret"), handler);
        await provider.EnsureBrowserClientAsync("members", "parallel-you-web", new string('a', 64), "consumer-secret", new Uri("https://forge.example/"));
        await provider.EnsureBrowserClientAsync("members", "parallel-you-web", new string('a', 64), "consumer-secret", new Uri("https://forge.example/"));
        var client = handler.Client!.Value;
        Assert.True(client.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.False(client.GetProperty("serviceAccountsEnabled").GetBoolean());
        Assert.False(client.GetProperty("directAccessGrantsEnabled").GetBoolean());
        Assert.Equal("https://forge.example/signin-oidc", client.GetProperty("redirectUris")[0].GetString());
        Assert.Equal("https://forge.example/signout-callback-oidc", client.GetProperty("attributes").GetProperty("post.logout.redirect.uris").GetString());
        Assert.Equal("S256", client.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString());
        Assert.Equal(1, handler.Created);
        Assert.Equal(1, handler.Updated);
    }

    [Fact]
    public async Task Existing_client_owned_by_another_deployment_is_not_modified()
    {
        var handler = new BrowserClientHandler();
        using var provider = new KeycloakResourceProvider(new RootCredential("identity.example", 443, "root", "root-secret"), handler);
        await provider.EnsureBrowserClientAsync("members", "parallel-you-web", new string('a', 64), "secret", new Uri("https://forge.example/"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.EnsureBrowserClientAsync("members", "parallel-you-web",
            new string('b', 64), "new-secret", new Uri("https://forge.example/")));
        Assert.Equal(0, handler.Updated);
    }

    [Theory]
    [InlineData("https://forge.example/path")]
    [InlineData("https://forge.example/?secret=1")]
    [InlineData("https://user:password@forge.example/")]
    [InlineData("http://forge.example/")]
    public async Task Invalid_origins_fail_before_contacting_the_identity_provider(string origin)
    {
        using var provider = new KeycloakResourceProvider(new RootCredential("identity.example", 443, "root", "root-secret"), new BrowserClientHandler());
        await Assert.ThrowsAsync<ArgumentException>(() => provider.EnsureBrowserClientAsync("members", "web", new string('a', 64), "secret", new Uri(origin)));
    }
}

internal sealed class DirectoryClientHandler : HttpMessageHandler
{
    public JsonElement? Client;
    public string[] Roles = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        string body;
        if (path.EndsWith("/token")) body = "{\"access_token\":\"test-token\"}";
        else if (request.Method == HttpMethod.Get && request.RequestUri.Query.Contains("realm-management")) body = "[{\"id\":\"management\"}]";
        else if (request.Method == HttpMethod.Get && request.RequestUri.Query.Contains("clientId=")) body = Client is null ? "[]" : "[{\"id\":\"directory\"}]";
        else if (path.EndsWith("/service-account-user")) body = "{\"id\":\"directory-account\"}";
        else if (path.EndsWith("/available")) body = "[{\"name\":\"view-users\"},{\"name\":\"query-users\"},{\"name\":\"query-groups\"},{\"name\":\"manage-users\"}]";
        else if (path.EndsWith("/role-mappings/clients/management"))
        {
            if (request.Method == HttpMethod.Post)
            {
                using var roles = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                Roles = roles.RootElement.EnumerateArray().Select(role => role.GetProperty("name").GetString()!).ToArray();
            }
            body = "[]";
        }
        else if (request.Method == HttpMethod.Get && path.EndsWith("/directory")) body = Client!.Value.GetRawText();
        else if (request.Method == HttpMethod.Post && path.EndsWith("/clients") || request.Method == HttpMethod.Put && path.EndsWith("/directory"))
        {
            using var client = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); Client = client.RootElement.Clone(); body = "{}";
        }
        else throw new InvalidOperationException("Unexpected directory call: " + path);
        return new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    }
}

internal sealed class BrowserClientHandler : HttpMessageHandler
{
    public JsonElement? Client { get; private set; }
    public int Created { get; private set; }
    public int Updated { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        string body;
        if (path.EndsWith("/token")) body = "{\"access_token\":\"test-token\"}";
        else if (request.Method == HttpMethod.Get && request.RequestUri.Query.Length != 0)
            body = Client is null ? "[]" : "[{\"id\":\"test-client-id\"}]";
        else if (request.Method == HttpMethod.Get && path.EndsWith("/test-client-id")) body = Client!.Value.GetRawText();
        else if (request.Method == HttpMethod.Post && path.EndsWith("/clients") || request.Method == HttpMethod.Put && path.EndsWith("/test-client-id"))
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Client = json.RootElement.Clone();
            if (request.Method == HttpMethod.Post) Created++; else Updated++;
            body = "{}";
        }
        else throw new InvalidOperationException("Unexpected identity-provider call: " + path);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    }
}
