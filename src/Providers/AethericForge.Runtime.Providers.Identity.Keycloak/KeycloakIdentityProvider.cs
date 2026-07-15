using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Identity.Primitives;
using IdentityModel.Client;
using System.IdentityModel.Tokens.Jwt;

namespace AethericForge.Runtime.Providers.Identity.Keycloak;

public sealed class KeycloakIdentityProvider : IIdentityProvider
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;

    public KeycloakIdentityProvider(HttpClient httpClient, KeycloakOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => "Keycloak";
    public IdentityScheme Scheme => IdentityScheme.OpenIdConnect;

    public async Task<IIdentitySubject?> ResolveSubjectAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        // Simple implementation: wrap the subjectId in an IdentitySubject.
        // In a real scenario, you might want to fetch more details from Keycloak's UserInfo endpoint or Admin API.
        return new IdentitySubject(subjectId, Scheme);
    }

    public async Task<IPrincipalIdentity?> AuthenticateAsync(
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken = default)
    {
        if (credentials.TryGetValue("token", out var token))
        {
            return await AuthenticateTokenAsync(token, cancellationToken);
        }

        if (credentials.TryGetValue("username", out var username) && 
            credentials.TryGetValue("password", out var password))
        {
            return await AuthenticatePasswordAsync(username, password, cancellationToken);
        }

        if (credentials.TryGetValue("subjectId", out var subjectId))
        {
            var subject = await ResolveSubjectAsync(subjectId, cancellationToken);
            return subject != null ? new PrincipalIdentity(subject, true) : null;
        }

        return null;
    }

    private async Task<IPrincipalIdentity?> AuthenticateTokenAsync(string token, CancellationToken cancellationToken)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return null;
        }

        var jwtToken = handler.ReadJwtToken(token);
        var subjectId = jwtToken.Subject;

        if (string.IsNullOrEmpty(subjectId))
        {
            return null;
        }

        var claims = jwtToken.Claims.Select(c => new IdentityClaim(c.Type, c.Value)).ToList();
        
        var subject = new IdentitySubject(
            subjectId,
            Scheme,
            jwtToken.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == "preferred_username")?.Value,
            IdentityState.Active,
            claims
        );

        return new PrincipalIdentity(subject, true, claims);
    }

    private async Task<IPrincipalIdentity?> AuthenticatePasswordAsync(string username, string password, CancellationToken cancellationToken)
    {
        var disco = await _httpClient.GetDiscoveryDocumentAsync(_options.Authority, cancellationToken);
        if (disco.IsError)
        {
            throw new InvalidOperationException($"Could not retrieve discovery document from {_options.Authority}: {disco.Error}");
        }

        var tokenResponse = await _httpClient.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = disco.TokenEndpoint,
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            UserName = username,
            Password = password
        }, cancellationToken);

        if (tokenResponse.IsError)
        {
            return null;
        }

        return await AuthenticateTokenAsync(tokenResponse.AccessToken!, cancellationToken);
    }
}
