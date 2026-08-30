using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;
using AethericForge.Runtime.Models.Identity.Directory;
using AethericForge.Runtime.Providers.Identity.Keycloak;
using System.Net;
using System.Text;

namespace AethericForge.Runtime.Tests.Identity.Directory;

public sealed class KeycloakExternalIdentityDirectoryTests
{
    [Fact]
    public async Task GetIdentity_UsesServiceAccountAndMapsKeycloakUser()
    {
        var handler = new StubHandler(request => request.Method == HttpMethod.Post
            ? Json(HttpStatusCode.OK, """{"access_token":"admin-token","expires_in":300}""")
            : Json(HttpStatusCode.OK, """
                {"id":"user-1","username":"ada","email":"ada@example.test","firstName":"Ada","lastName":"Lovelace","enabled":false,"attributes":{"department":["mathematics"]}}
                """));
        using var directory = CreateDirectory(handler);

        var result = await directory.GetIdentityAsync(
            new ExternalIdentityReference("keycloak", "campus", "user-1"));

        Assert.Equal(ExternalDirectoryStatus.Success, result.Status);
        Assert.Equal("Ada Lovelace", result.Value!.DisplayName);
        Assert.False(result.Value.IsEnabled);
        Assert.Equal("ada@example.test", result.Value.Properties["email"]);
        Assert.Equal("mathematics", result.Value.Properties["department"]);
        Assert.Equal("https://id.example/realms/campus/protocol/openid-connect/token", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal("https://id.example/admin/realms/campus/users/user-1", handler.Requests[1].RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", handler.Requests[1].Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task GetGroups_ReturnsDeterministicallyOrderedKeycloakIds()
    {
        var handler = AuthenticatedHandler("""[{"id":"group-z"},{"id":"group-a"}]""");
        using var directory = CreateDirectory(handler);

        var result = await directory.GetGroupsAsync(
            new ExternalIdentityReference("Keycloak", "campus", "user-1"));

        Assert.Equal(["group-a", "group-z"], result.Value!.Select(group => group.GroupId));
        Assert.Contains("/users/user-1/groups", handler.Requests[1].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetGroupMembers_ReturnsDisabledUsersInSubjectOrder()
    {
        var handler = AuthenticatedHandler("""
            [{"id":"user-2","username":"grace","enabled":true},{"id":"user-1","username":"ada","enabled":false}]
            """);
        using var directory = CreateDirectory(handler);

        var result = await directory.GetGroupMembersAsync(
            new ExternalGroupReference("Keycloak", "campus", "group-1"));

        Assert.Equal(["user-1", "user-2"], result.Value!.Select(member => member.Reference.SubjectId));
        Assert.False(result.Value!.First().IsEnabled);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ExternalDirectoryStatus.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, ExternalDirectoryStatus.Untrusted)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ExternalDirectoryStatus.Unavailable)]
    public async Task AdminFailures_AreRepresentedWithoutThrowing(
        HttpStatusCode responseStatus,
        ExternalDirectoryStatus expectedStatus)
    {
        var handler = new StubHandler(request => request.Method == HttpMethod.Post
            ? Json(HttpStatusCode.OK, """{"access_token":"token","expires_in":300}""")
            : Json(responseStatus, "failure"));
        using var directory = CreateDirectory(handler);

        var result = await directory.GetIdentityAsync(
            new ExternalIdentityReference("Keycloak", "campus", "missing"));

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Value);
        Assert.Equal("failure", result.FailureReason);
    }

    [Fact]
    public async Task ForeignReferences_AreRejectedBeforeCallingKeycloak()
    {
        var handler = AuthenticatedHandler("{}");
        using var directory = CreateDirectory(handler);

        var result = await directory.GetIdentityAsync(
            new ExternalIdentityReference("Keycloak", "another-realm", "user-1"));

        Assert.Equal(ExternalDirectoryStatus.Untrusted, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AccessToken_IsReusedWhileFresh()
    {
        var handler = AuthenticatedHandler("""{"id":"user-1","username":"ada","enabled":true}""");
        using var directory = CreateDirectory(handler);
        var reference = new ExternalIdentityReference("Keycloak", "campus", "user-1");

        await directory.GetIdentityAsync(reference);
        await directory.GetIdentityAsync(reference);

        Assert.Single(handler.Requests.Where(request => request.Method == HttpMethod.Post));
        Assert.Equal(2, handler.Requests.Count(request => request.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task ResolveGroup_TranslatesAnExactNameToItsKeycloakId()
    {
        var handler = AuthenticatedHandler("""
            [{"id":"parent","name":"teams","subGroups":[{"id":"member-uuid","name":"adr-campus-members"}]}]
            """);
        using var directory = CreateDirectory(handler);

        var result = await directory.ResolveGroupAsync("adr-campus-members");

        Assert.Equal(ExternalDirectoryStatus.Success, result.Status);
        Assert.Equal("member-uuid", result.Value!.GroupId);
        Assert.Contains("search=adr-campus-members", handler.Requests[1].RequestUri!.Query);
        Assert.Contains("exact=true", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task ResolveGroup_RejectsAnAmbiguousName()
    {
        var handler = AuthenticatedHandler("""
            [{"id":"group-1","name":"members"},{"id":"group-2","name":"members"}]
            """);
        using var directory = CreateDirectory(handler);

        var result = await directory.ResolveGroupAsync("members");

        Assert.Equal(ExternalDirectoryStatus.Misconfigured, result.Status);
        Assert.Null(result.Value);
        Assert.Contains("More than one", result.FailureReason);
    }

    private static KeycloakExternalIdentityDirectory CreateDirectory(StubHandler handler) =>
        new(new HttpClient(handler), new KeycloakOptions
        {
            Authority = "https://id.example/realms/campus",
            Realm = "campus",
            ClientId = "runtime",
            ClientSecret = "secret"
        });

    private static StubHandler AuthenticatedHandler(string adminResponse) =>
        new(request => request.Method == HttpMethod.Post
            ? Json(HttpStatusCode.OK, """{"access_token":"token","expires_in":300}""")
            : Json(HttpStatusCode.OK, adminResponse));

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(response(request));
        }
    }
}
