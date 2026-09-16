using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AethericForge.Runtime.Providers.Identity.Keycloak;

/// <summary>
/// Shared service-account token acquisition and admin-realm URI construction for the Keycloak
/// Admin REST API. Used by both the read side (<see cref="KeycloakExternalIdentityDirectory"/>)
/// and the write side (<see cref="KeycloakRegistryClerk"/>) - each keeps its own request/response
/// shapes and error-status mapping (they map to different result enums), but neither should
/// re-implement client_credentials token caching, which is easy to get subtly wrong (races,
/// expiry skew) and not worth risking twice.
/// </summary>
internal sealed class KeycloakAdminAccess : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Uri _tokenEndpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAtUtc;

    public KeycloakAdminAccess(HttpClient httpClient, KeycloakOptions options, TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(options);

        var serverBase = RequiredAbsoluteUri(options.Authority, nameof(options.Authority));
        _clientId = Required(options.ClientId, nameof(options.ClientId));
        _clientSecret = Required(options.ClientSecret, nameof(options.ClientSecret));
        Realm = Required(options.Realm, nameof(options.Realm));

        _timeProvider = timeProvider ?? TimeProvider.System;
        var realmAuthority = new Uri(EnsureTrailingSlash(serverBase), $"realms/{Uri.EscapeDataString(Realm)}/");
        _tokenEndpoint = new Uri(realmAuthority, "protocol/openid-connect/token");
        var adminBase = string.IsNullOrWhiteSpace(options.AdminApiBaseAddress)
            ? new Uri(EnsureTrailingSlash(serverBase), "admin/")
            : RequiredAbsoluteUri(options.AdminApiBaseAddress, nameof(options.AdminApiBaseAddress));
        AdminRealmEndpoint = new Uri(EnsureTrailingSlash(adminBase), $"realms/{Uri.EscapeDataString(Realm)}/");
    }

    public string Realm { get; }
    public Uri AdminRealmEndpoint { get; }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var now = Now();
        if (_accessToken is not null && _accessTokenExpiresAtUtc > now.AddSeconds(15))
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = Now();
            if (_accessToken is not null && _accessTokenExpiresAtUtc > now.AddSeconds(15))
            {
                return _accessToken;
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            });
            using var response = await _httpClient.PostAsync(_tokenEndpoint, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new KeycloakAdminAccessException(
                    response.StatusCode,
                    await FailureReasonAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var token = await response.Content
                .ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
            {
                throw new KeycloakAdminAccessException(null, "Keycloak did not return an access token.");
            }

            _accessToken = token.AccessToken;
            _accessTokenExpiresAtUtc = now.AddSeconds(Math.Max(0, token.ExpiresIn));
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public void Dispose() => _tokenLock.Dispose();

    public static async Task<string?> FailureReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var reason = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        reason = string.IsNullOrWhiteSpace(reason) ? response.ReasonPhrase : reason.Trim();
        return reason is { Length: > 2048 } ? reason[..2048] : reason;
    }

    private DateTimeOffset Now() => _timeProvider.GetUtcNow();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static Uri EnsureTrailingSlash(Uri value) =>
        value.AbsoluteUri.EndsWith('/') ? value : new Uri(value.AbsoluteUri + "/");

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameterName);
        return value.Trim();
    }

    private static Uri RequiredAbsoluteUri(string value, string parameterName)
    {
        if (!Uri.TryCreate(Required(value, parameterName), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("An absolute HTTP or HTTPS URI is required.", parameterName);
        }
        return uri;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    }
}

/// <summary>Thrown only while acquiring a service-account token - callers map this to their own result/status shape.</summary>
internal sealed class KeycloakAdminAccessException(HttpStatusCode? statusCode, string? message) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
