using AethericForge.Runtime.Abstractions.Interfaces.Identity.Primitives;
using AethericForge.Runtime.Providers.Identity.Keycloak;
using AethericForge.Runtime.Services.Identity;
using Xunit;

namespace AethericForge.Runtime.Tests;

public class IdentityTests
{
    [Fact]
    public async Task Keycloak_AuthenticateAsync_ReturnsPrincipal_WhenCredentialsAreValid()
    {
        // Arrange
        var options = new KeycloakOptions
        {
            Authority = "http://localhost:8080/realms/Aetheric",
            ClientId = "runtime-api",
            ClientSecret = "test-secret",
            Realm = "Aetheric"
        };

        using var httpClient = new HttpClient();
        var provider = new KeycloakIdentityProvider(httpClient, options);
        var identityService = new IdentityService(new[] { provider });

        var credentials = new Dictionary<string, string>
        {
            { "username", "testuser" },
            { "password", "password" }
        };

        // Act
        var principal = await identityService.AuthenticateAsync(IdentityScheme.OpenIdConnect, credentials);

        // Assert
        Assert.NotNull(principal);
        Assert.True(principal.IsAuthenticated);
        // Keycloak usually sets preferred_username to the username
        Assert.Contains(principal.Subject.Claims, c => c.Type == "preferred_username" && c.Value == "testuser");
    }

    [Fact]
    public async Task Keycloak_ResolveSubjectAsync_ReturnsSubject()
    {
        // Arrange
        var options = new KeycloakOptions
        {
            Authority = "http://localhost:8080/realms/Aetheric",
            ClientId = "runtime-api",
            ClientSecret = "test-secret",
            Realm = "Aetheric"
        };

        using var httpClient = new HttpClient();
        var provider = new KeycloakIdentityProvider(httpClient, options);
        
        var subjectId = "some-id";

        // Act
        var subject = await provider.ResolveSubjectAsync(subjectId);

        // Assert
        Assert.NotNull(subject);
        Assert.Equal(subjectId, subject.SubjectId);
        Assert.Equal(IdentityScheme.OpenIdConnect, subject.Scheme);
    }
}
