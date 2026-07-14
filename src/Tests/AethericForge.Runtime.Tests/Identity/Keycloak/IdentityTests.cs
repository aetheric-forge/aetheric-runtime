using System.Net;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using IdentityModel.Client;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Providers.Identity.Keycloak;
using AethericForge.Runtime.Services.Identity;
using AethericForge.Runtime.Services.Identity.Lifecycle;
using Xunit;

namespace AethericForge.Runtime.Tests.Identity.Keycloak;

public class IdentityTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public required Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Handler(request, cancellationToken);
    }

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

        var handler = new MockHttpMessageHandler
        {
            Handler = (request, ct) =>
            {
                var uri = request.RequestUri!.ToString();
                
                if (uri.Contains(".well-known/openid-configuration"))
                {
                    var disco = new
                    {
                        issuer = "http://localhost:8080/realms/Aetheric",
                        token_endpoint = "http://localhost:8080/realms/Aetheric/protocol/openid-connect/token",
                        userinfo_endpoint = "http://localhost:8080/realms/Aetheric/protocol/openid-connect/userinfo",
                        jwks_uri = "http://localhost:8080/realms/Aetheric/protocol/openid-connect/certs"
                    };
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(disco), System.Text.Encoding.UTF8, "application/json")
                    });
                }

                if (uri.Contains("protocol/openid-connect/certs"))
                {
                    var jwks = new { keys = new object[] { } };
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(jwks), System.Text.Encoding.UTF8, "application/json")
                    });
                }

                if (uri.Contains("protocol/openid-connect/token"))
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var tokenDescriptor = new SecurityTokenDescriptor
                    {
                        Subject = new ClaimsIdentity(new[] 
                        { 
                            new Claim("sub", "testuser-id"),
                            new Claim("preferred_username", "testuser"),
                            new Claim("email", "test@example.com")
                        }),
                        Issuer = "http://localhost:8080/realms/Aetheric",
                        Audience = "runtime-api"
                    };
                    var token = tokenHandler.CreateEncodedJwt(tokenDescriptor);

                    var tokenResponse = new
                    {
                        access_token = token,
                        expires_in = 3600,
                        token_type = "Bearer"
                    };
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(tokenResponse), System.Text.Encoding.UTF8, "application/json")
                    });
                }

                if (uri.Contains("protocol/openid-connect/userinfo"))
                {
                    var userinfo = new
                    {
                        sub = "testuser-id",
                        preferred_username = "testuser",
                        email = "test@example.com"
                    };
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(userinfo), System.Text.Encoding.UTF8, "application/json")
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        };

        using var httpClient = new HttpClient(handler);
        var provider = new KeycloakIdentityProvider(httpClient, options);
        var lifecycleService = new IdentityLifecycleService(Enumerable.Empty<IIdentityLifecyclePolicy>());
        var identityService = new IdentityService(new[] { provider }, lifecycleService);

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
        Assert.Equal("testuser-id", principal.SubjectId);
        Assert.Contains(principal.Claims, c => c.Type == "preferred_username" && c.Value == "testuser");
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
