using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.Keycloak;

/// <summary>
/// Live-verifies the one parent contract this University/Campus hierarchy currently has: a
/// Campus's dependency on its University's shared Registry ("IRegistrar"). Deliberately narrow -
/// one contract, one convention (a realm name supplied by the caller) - rather than a generic
/// resolver for arbitrary future parent contracts. SimulatedCatalogParentResolver and
/// SimulatedParentResolver (Aetheric.Provisioning.Simulation) are explicitly documented as fake
/// ("an advertised catalog is not evidence of live provider access") - this is the first real
/// one, and it earns that by actually calling Keycloak's admin API, not by trusting
/// ParentContext.Capabilities.
/// </summary>
public sealed class KeycloakRealmParentCapabilityResolver : IParentCapabilityResolver, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _username;
    private readonly string _password;
    private readonly string _tokenRealm;
    private readonly string _tokenClientId;
    private readonly string _realm;
    private string? _token;

    public KeycloakRealmParentCapabilityResolver(RootCredential rootCredential, string realm, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(rootCredential);
        if (string.IsNullOrWhiteSpace(realm)) throw new ArgumentException("A realm is required.", nameof(realm));
        _username = rootCredential.Username ?? throw new ArgumentException("Root credential requires a username.", nameof(rootCredential));
        _password = rootCredential.Password;
        var options = rootCredential.Keycloak ?? new KeycloakRootOptions();
        var origin = new UriBuilder(options.Scheme, rootCredential.Host, rootCredential.Port, options.BasePath).Uri;
        _tokenRealm = options.Realm;
        _tokenClientId = options.ClientId;
        _realm = realm;
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = origin };
    }

    public async Task<bool> IsAvailableAsync(ParentContext parent, string contract, string source, CancellationToken cancellationToken)
    {
        if (contract != "IRegistrar") return false;
        await AuthenticateAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"admin/realms/{Uri.EscapeDataString(_realm)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    private async Task AuthenticateAsync(CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["client_id"] = _tokenClientId, ["username"] = _username, ["password"] = _password,
        });
        using var response = await _http.PostAsync($"realms/{Uri.EscapeDataString(_tokenRealm)}/protocol/openid-connect/token", form, ct);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
        _token = token?.AccessToken;
        if (string.IsNullOrWhiteSpace(_token)) throw new InvalidOperationException("Keycloak did not return an access token.");
    }

    public void Dispose() => _http.Dispose();

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
    }
}
