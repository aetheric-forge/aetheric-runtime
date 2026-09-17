using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Clients;
using AethericForge.Runtime.Models.Identity.Authorization;
using AethericForge.Runtime.Providers.Identity.Keycloak;
using System.Net;
using System.Text;

namespace AethericForge.Runtime.Tests.Identity.Keycloak;

public sealed class KeycloakRegistryClerkTests
{
    [Fact]
    public async Task GetClient_ReturnsTheClientWithoutFetchingItsSecret()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "clients", Json(HttpStatusCode.OK, """
            [{"id":"internal-1","clientId":"aetheric-admin","enabled":true,"publicClient":false}]
            """));
        using var clerk = CreateClerk(handler);

        var result = await clerk.GetClientAsync("aetheric-admin");

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Equal("aetheric-admin", result.Value!.ClientId);
        Assert.Null(result.Value.Secret);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.AbsolutePath.Contains("client-secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetClient_AnUnknownClientIdIsNotFound()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "clients", Json(HttpStatusCode.OK, "[]"));
        using var clerk = CreateClerk(handler);

        var result = await clerk.GetClientAsync("missing-client");

        Assert.Equal(RegistryOperationStatus.NotFound, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRole_ReturnsAnExistingRole()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "roles/provisioner-admin", Json(HttpStatusCode.OK, """{"id":"role-1","name":"provisioner-admin"}"""));
        using var clerk = CreateClerk(handler);

        var result = await clerk.GetRoleAsync("provisioner-admin");

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Equal("provisioner-admin", result.Value!.Name);
    }

    [Fact]
    public async Task GetRole_AnUnknownRoleIsNotFound()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "roles/ghost-role", Json(HttpStatusCode.NotFound, "not found"));
        using var clerk = CreateClerk(handler);

        var result = await clerk.GetRoleAsync("ghost-role");

        Assert.Equal(RegistryOperationStatus.NotFound, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetGroup_ReturnsAnExistingGroupByExactPath()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "groups", Json(HttpStatusCode.OK, """[{"id":"group-1","name":"teams","path":"/teams"}]"""));
        using var clerk = CreateClerk(handler);

        var result = await clerk.GetGroupAsync("/teams");

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Equal("teams", result.Value!.Name);
        Assert.Equal("/teams", result.Value.Path);
    }

    [Fact]
    public async Task GetGroup_AnUnknownPathIsNotFound()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "groups", Json(HttpStatusCode.OK, "[]"));
        using var clerk = CreateClerk(handler);

        var result = await clerk.GetGroupAsync("/does-not-exist");

        Assert.Equal(RegistryOperationStatus.NotFound, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task RegisterClient_CreatesAConfidentialClientAndReturnsItsSecret()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Post, "clients", Json(HttpStatusCode.Created, "{}"));
        // Registered before the generic "clients" GET route below - route matching is
        // first-registered-wins on a path substring, and "clients" is itself a substring of
        // the client-secret URL, so the more specific route must win the race.
        handler.On(HttpMethod.Get, "client-secret", Json(HttpStatusCode.OK, """{"type":"secret","value":"s3cr3t"}"""));
        handler.On(HttpMethod.Get, "clients", Json(HttpStatusCode.OK, """
            [{"id":"internal-1","clientId":"aetheric-admin","enabled":true,"publicClient":false,
              "standardFlowEnabled":true,"directAccessGrantsEnabled":false,
              "redirectUris":["https://app.example/signin-oidc"],"webOrigins":["https://app.example"]}]
            """));
        using var clerk = CreateClerk(handler);

        var result = await clerk.RegisterClientAsync(new ClientRegistrationRequest(
            "aetheric-admin",
            RedirectUris: ["https://app.example/signin-oidc"],
            WebOrigins: ["https://app.example"]));

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Equal("aetheric-admin", result.Value!.ClientId);
        Assert.Equal("s3cr3t", result.Value.Secret);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("/clients", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RegisterClient_APublicClientNeverFetchesASecret()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Post, "clients", Json(HttpStatusCode.Created, "{}"));
        handler.On(HttpMethod.Get, "clients", Json(HttpStatusCode.OK, """
            [{"id":"internal-1","clientId":"parallel-you-spa","enabled":true,"publicClient":true}]
            """));
        using var clerk = CreateClerk(handler);

        var result = await clerk.RegisterClientAsync(new ClientRegistrationRequest("parallel-you-spa", PublicClient: true));

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Null(result.Value!.Secret);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.AbsolutePath.Contains("client-secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RegisterClient_ADuplicateClientIdIsReportedAsAlreadyExists()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Post, "clients", Json(HttpStatusCode.Conflict, "Client already exists"));
        using var clerk = CreateClerk(handler);

        var result = await clerk.RegisterClientAsync(new ClientRegistrationRequest("forge-campus"));

        Assert.Equal(RegistryOperationStatus.AlreadyExists, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task UpdateClient_AnUnknownClientIdIsNotFound()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "clients", Json(HttpStatusCode.OK, "[]"));
        using var clerk = CreateClerk(handler);

        var result = await clerk.UpdateClientAsync("missing-client", new ClientRegistrationRequest("missing-client"));

        Assert.Equal(RegistryOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteClient_ResolvesTheInternalIdBeforeDeleting()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "clients", Json(HttpStatusCode.OK, """[{"id":"internal-9","clientId":"old-client"}]"""));
        handler.On(HttpMethod.Delete, "clients/internal-9", new HttpResponseMessage(HttpStatusCode.NoContent));
        using var clerk = CreateClerk(handler);

        var result = await clerk.DeleteClientAsync("old-client");

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Delete && r.RequestUri!.AbsolutePath.EndsWith("/clients/internal-9", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateRole_Succeeds()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Post, "roles", new HttpResponseMessage(HttpStatusCode.Created));
        using var clerk = CreateClerk(handler);

        var result = await clerk.CreateRoleAsync("campus-members", "Active campus members");

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Equal("campus-members", result.Value!.Name);
    }

    [Fact]
    public async Task CreateRole_ADuplicateNameIsReportedAsAlreadyExists()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Post, "roles", Json(HttpStatusCode.Conflict, "Role already exists"));
        using var clerk = CreateClerk(handler);

        var result = await clerk.CreateRoleAsync("campus-members");

        Assert.Equal(RegistryOperationStatus.AlreadyExists, result.Status);
    }

    [Fact]
    public async Task DeleteRole_AnUnknownRoleIsNotFound()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Delete, "roles", Json(HttpStatusCode.NotFound, "not found"));
        using var clerk = CreateClerk(handler);

        var result = await clerk.DeleteRoleAsync("ghost-role");

        Assert.Equal(RegistryOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task CreateGroup_ATopLevelGroupGetsARootPath()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Post, "groups", new HttpResponseMessage(HttpStatusCode.Created));
        using var clerk = CreateClerk(handler);

        var result = await clerk.CreateGroupAsync("teams");

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Equal("/teams", result.Value!.Path);
    }

    [Fact]
    public async Task CreateGroup_ANestedGroupResolvesItsParentFirst()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "groups", Json(HttpStatusCode.OK, """[{"id":"parent-1","name":"teams","path":"/teams"}]"""));
        handler.On(HttpMethod.Post, "groups/parent-1/children", new HttpResponseMessage(HttpStatusCode.Created));
        using var clerk = CreateClerk(handler);

        var result = await clerk.CreateGroupAsync("adr-campus-members", "/teams");

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Equal("/teams/adr-campus-members", result.Value!.Path);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("/groups/parent-1/children", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateGroup_AnUnknownParentPathIsNotFound()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "groups", Json(HttpStatusCode.OK, "[]"));
        using var clerk = CreateClerk(handler);

        var result = await clerk.CreateGroupAsync("orphan", "/does-not-exist");

        Assert.Equal(RegistryOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task AssignRoleToGroup_ResolvesBothThenPostsTheRoleMapping()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "roles/campus-members", Json(HttpStatusCode.OK, """{"id":"role-1","name":"campus-members"}"""));
        handler.On(HttpMethod.Get, "groups", Json(HttpStatusCode.OK, """[{"id":"group-1","name":"teams","path":"/teams"}]"""));
        handler.On(HttpMethod.Post, "role-mappings/realm", new HttpResponseMessage(HttpStatusCode.NoContent));
        using var clerk = CreateClerk(handler);

        var result = await clerk.AssignRoleToGroupAsync("campus-members", "/teams");

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath.EndsWith("/groups/group-1/role-mappings/realm", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssignRoleToPrincipal_ResolvesTheRoleThenPostsTheUserRoleMapping()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "roles/campus-members", Json(HttpStatusCode.OK, """{"id":"role-1","name":"campus-members"}"""));
        handler.On(HttpMethod.Post, "role-mappings/realm", new HttpResponseMessage(HttpStatusCode.NoContent));
        using var clerk = CreateClerk(handler);

        var result = await clerk.AssignRoleToPrincipalAsync("campus-members", "user-1");

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath.EndsWith("/users/user-1/role-mappings/realm", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssignPermissionsToRole_AutoCreatesAMissingPermissionRoleThenComposesIt()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "roles/editor", Json(HttpStatusCode.OK, """{"id":"role-editor","name":"editor"}"""));
        var permissionRoleCalls = 0;
        handler.On(HttpMethod.Get, "roles/archive:read", request =>
        {
            permissionRoleCalls++;
            return permissionRoleCalls == 1
                ? Json(HttpStatusCode.NotFound, "not found")
                : Json(HttpStatusCode.OK, """{"id":"role-permission","name":"archive:read"}""");
        });
        // Registered before the generic "roles" POST route below - "roles" is itself a
        // substring of the composites URL, so the more specific route must win the race.
        handler.On(HttpMethod.Post, "composites", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.On(HttpMethod.Post, "roles", new HttpResponseMessage(HttpStatusCode.Created));
        using var clerk = CreateClerk(handler);

        var result = await clerk.AssignPermissionsToRoleAsync(
            "editor",
            [new Permission("archive", "read")]);

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("/roles/editor/composites", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssignPermissionsToRole_ReusesAnExistingPermissionRoleWithoutRecreatingIt()
    {
        var handler = new RoutedStubHandler();
        handler.OnToken();
        handler.On(HttpMethod.Get, "roles/editor", Json(HttpStatusCode.OK, """{"id":"role-editor","name":"editor"}"""));
        handler.On(HttpMethod.Get, "roles/archive:read", Json(HttpStatusCode.OK, """{"id":"role-permission","name":"archive:read"}"""));
        handler.On(HttpMethod.Post, "composites", new HttpResponseMessage(HttpStatusCode.NoContent));
        using var clerk = CreateClerk(handler);

        var result = await clerk.AssignPermissionsToRoleAsync("editor", [new Permission("archive", "read")]);

        Assert.Equal(RegistryOperationStatus.Succeeded, result.Status);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("/roles", StringComparison.Ordinal));
    }

    private static KeycloakRegistryClerk CreateClerk(RoutedStubHandler handler) =>
        new(new HttpClient(handler), new KeycloakOptions
        {
            Authority = "https://id.example",
            Realm = "campus",
            ClientId = "runtime",
            ClientSecret = "secret"
        });

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    /// <summary>
    /// Dispatches by HTTP method + a path fragment, in registration order (first match wins) -
    /// the write flows here make several distinct calls per test (resolve role, resolve group,
    /// post mapping...), unlike the read side's single-response-per-test shape.
    /// </summary>
    private sealed class RoutedStubHandler : HttpMessageHandler
    {
        private readonly List<(HttpMethod Method, string PathFragment, Func<HttpRequestMessage, HttpResponseMessage> Respond)> routes = [];
        public List<HttpRequestMessage> Requests { get; } = [];

        public void OnToken() =>
            routes.Add((HttpMethod.Post, "protocol/openid-connect/token", _ => Json(HttpStatusCode.OK, """{"access_token":"token","expires_in":300}""")));

        public void On(HttpMethod method, string pathFragment, HttpResponseMessage response) =>
            routes.Add((method, pathFragment, _ => response));

        public void On(HttpMethod method, string pathFragment, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            routes.Add((method, pathFragment, respond));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
            foreach (var route in routes)
            {
                if (route.Method == request.Method && path.Contains(route.PathFragment, StringComparison.Ordinal))
                {
                    return Task.FromResult(route.Respond(request));
                }
            }

            throw new InvalidOperationException($"No stub route for {request.Method} {path}.");
        }
    }
}
