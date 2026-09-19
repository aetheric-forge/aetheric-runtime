using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.Keycloak;

/// <summary>
/// Ensures a Keycloak realm exists and a resource-scoped, service-account-enabled client exists
/// within it, granted the realm-management roles needed to manage users/roles in that realm - the
/// same admin surface AethericForge.Runtime.Providers.Identity.Keycloak's KeycloakRegistryClerk
/// exercises at runtime, so the client this provider creates is immediately usable as that
/// runtime's own KeycloakOptions. The host supplies a root admin username/password (the same
/// authentication method kcadm.sh itself uses) - connections never enter a plan.
/// </summary>
public sealed class KeycloakResourceProvider : IResourceProvider, IDisposable
{
    private static readonly Regex RealmPattern = new(@"\A[a-zA-Z0-9_-]{1,36}\z", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] RealmManagementRoles = ["manage-users", "view-users"];

    private readonly HttpClient _http;
    private readonly string _username;
    private readonly string _password;
    private readonly string _tokenRealm;
    private readonly string _tokenClientId;
    private string? _token;

    public KeycloakResourceProvider(RootCredential rootCredential, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(rootCredential);
        _username = rootCredential.Username ?? throw new ArgumentException("Root credential requires a username.", nameof(rootCredential));
        _password = rootCredential.Password;
        var options = rootCredential.Keycloak ?? new KeycloakRootOptions();
        var origin = new UriBuilder(options.Scheme, rootCredential.Host, rootCredential.Port, options.BasePath).Uri;
        _tokenRealm = options.Realm;
        _tokenClientId = options.ClientId;
        // handler is a test seam - production callers never pass one, so this always owns and
        // disposes a real HttpClientHandler with auto-redirect off.
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = origin };
    }

    public string Key => "keycloak";

    public ImmutableArray<ValidationIssue> Validate(ResourceRequirement resource, ResourceBinding binding)
    {
        var valid = resource.Ownership == "owned" && binding.Provider == Key
            && binding.Settings.TryGetValue("realm", out var realm) && RealmPattern.IsMatch(realm)
            && binding.Settings.Keys.All(k => k is "realm")
            && binding.Secrets.IsEmpty;
        return valid ? [] : [new("keycloak.binding", resource.Id,
            "Keycloak requires an owned resource with only a realm setting (1-36 characters: letters, digits, '_', '-'). Credentials are supplied by the host.")];
    }

    public async Task<ProviderResult> EnsureAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        if (!Validate(context.Resource, context.Binding).IsEmpty)
            throw new InvalidOperationException("Invalid Keycloak binding.");

        var realm = context.Binding.Settings["realm"];
        var scopedClientId = $"{context.InstitutionId}-{context.Resource.Id}";

        await AuthenticateAsync(cancellationToken);
        if (!await RealmExistsAsync(realm, cancellationToken))
            await PostAsync("admin/realms", new RealmRepresentation { Realm = realm, Enabled = true }, cancellationToken);

        var secret = await context.Secrets.GetOrCreateAsync("keycloak", $"{realm}-{scopedClientId}", cancellationToken);
        var password = await context.Secrets.ReadAsync(secret, cancellationToken);

        var existingId = await FindClientInternalIdAsync(realm, scopedClientId, cancellationToken);
        var alreadyExists = existingId is not null;
        var internalId = existingId ?? await CreateClientAsync(realm, scopedClientId, password, cancellationToken);
        if (alreadyExists) await SetClientSecretAsync(realm, internalId, password, cancellationToken);

        await EnsureServiceAccountRolesAsync(realm, internalId, cancellationToken);

        return new(alreadyExists, [secret]);
    }

    private async Task<bool> RealmExistsAsync(string realm, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, $"admin/realms/{Uri.EscapeDataString(realm)}", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    private async Task<string?> FindClientInternalIdAsync(string realm, string clientId, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get,
            $"admin/realms/{Uri.EscapeDataString(realm)}/clients?clientId={Uri.EscapeDataString(clientId)}&exact=true", null, ct);
        response.EnsureSuccessStatusCode();
        var matches = await response.Content.ReadFromJsonAsync<List<ClientRepresentation>>(JsonOptions, ct);
        return matches?.SingleOrDefault()?.Id;
    }

    private async Task<string> CreateClientAsync(string realm, string clientId, string password, CancellationToken ct)
    {
        var representation = new ClientRepresentation
        {
            ClientId = clientId,
            Enabled = true,
            PublicClient = false,
            ServiceAccountsEnabled = true,
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            ClientAuthenticatorType = "client-secret",
            Secret = password,
        };
        using var create = await SendAsync(HttpMethod.Post, $"admin/realms/{Uri.EscapeDataString(realm)}/clients", representation, ct);
        create.EnsureSuccessStatusCode();
        var internalId = await FindClientInternalIdAsync(realm, clientId, ct);
        if (internalId is null) throw new InvalidOperationException("Keycloak did not report the client it just created.");
        return internalId;
    }

    private async Task SetClientSecretAsync(string realm, string internalId, string password, CancellationToken ct)
    {
        // A re-run must reassert the same password the secret store already returned - PUT the
        // whole representation with the new secret rather than relying on Keycloak's own
        // secret-regeneration endpoint, which would mint a value our secret store doesn't know.
        using var update = await SendAsync(HttpMethod.Put, $"admin/realms/{Uri.EscapeDataString(realm)}/clients/{Uri.EscapeDataString(internalId)}",
            new ClientRepresentation { Id = internalId, Secret = password }, ct);
        update.EnsureSuccessStatusCode();
    }

    private async Task EnsureServiceAccountRolesAsync(string realm, string internalId, CancellationToken ct)
    {
        using var accountResponse = await SendAsync(HttpMethod.Get,
            $"admin/realms/{Uri.EscapeDataString(realm)}/clients/{Uri.EscapeDataString(internalId)}/service-account-user", null, ct);
        accountResponse.EnsureSuccessStatusCode();
        var account = await accountResponse.Content.ReadFromJsonAsync<UserRepresentation>(JsonOptions, ct);
        var userId = account?.Id ?? throw new InvalidOperationException("Keycloak did not report the client's service-account user.");

        using var realmMgmtResponse = await SendAsync(HttpMethod.Get,
            $"admin/realms/{Uri.EscapeDataString(realm)}/clients?clientId=realm-management&exact=true", null, ct);
        realmMgmtResponse.EnsureSuccessStatusCode();
        var realmMgmtId = (await realmMgmtResponse.Content.ReadFromJsonAsync<List<ClientRepresentation>>(JsonOptions, ct))?.SingleOrDefault()?.Id
            ?? throw new InvalidOperationException("Keycloak realm has no realm-management client.");

        using var assignedResponse = await SendAsync(HttpMethod.Get,
            $"admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(userId)}/role-mappings/clients/{Uri.EscapeDataString(realmMgmtId)}", null, ct);
        assignedResponse.EnsureSuccessStatusCode();
        var assigned = (await assignedResponse.Content.ReadFromJsonAsync<List<RoleRepresentation>>(JsonOptions, ct))
            ?.Select(r => r.Name).ToHashSet(StringComparer.Ordinal) ?? [];
        if (RealmManagementRoles.All(assigned.Contains)) return;

        using var availableResponse = await SendAsync(HttpMethod.Get,
            $"admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(userId)}/role-mappings/clients/{Uri.EscapeDataString(realmMgmtId)}/available", null, ct);
        availableResponse.EnsureSuccessStatusCode();
        var available = await availableResponse.Content.ReadFromJsonAsync<List<RoleRepresentation>>(JsonOptions, ct) ?? [];
        var toAssign = available.Where(r => r.Name is not null && RealmManagementRoles.Contains(r.Name) && !assigned.Contains(r.Name)).ToList();
        if (toAssign.Count == 0) return;

        using var assign = await SendAsync(HttpMethod.Post,
            $"admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(userId)}/role-mappings/clients/{Uri.EscapeDataString(realmMgmtId)}", toAssign, ct);
        assign.EnsureSuccessStatusCode();
    }

    private async Task AuthenticateAsync(CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["client_id"] = _tokenClientId, ["username"] = _username, ["password"] = _password,
        });
        using var response = await _http.PostAsync($"realms/{Uri.EscapeDataString(_tokenRealm)}/protocol/openid-connect/token", form, ct);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, ct);
        _token = token?.AccessToken;
        if (string.IsNullOrWhiteSpace(_token)) throw new InvalidOperationException("Keycloak did not return an access token.");
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object body, CancellationToken ct) =>
        await SendAsync(HttpMethod.Post, path, body, ct);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        var response = await _http.SendAsync(request, ct);
        // NotFound is a legitimate "doesn't exist yet" answer some callers check for explicitly
        // (RealmExistsAsync) - every other caller only ever expects success, so EnsureSuccessStatusCode
        // still applies to any other non-2xx response.
        if (response.StatusCode != HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
        return response;
    }

    public void Dispose() => _http.Dispose();

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
    }

    private sealed record RealmRepresentation
    {
        public string? Realm { get; init; }
        public bool? Enabled { get; init; }
    }

    private sealed record ClientRepresentation
    {
        public string? Id { get; init; }
        public string? ClientId { get; init; }
        public bool? Enabled { get; init; }
        public bool? PublicClient { get; init; }
        public bool? ServiceAccountsEnabled { get; init; }
        public bool? StandardFlowEnabled { get; init; }
        public bool? DirectAccessGrantsEnabled { get; init; }
        public string? ClientAuthenticatorType { get; init; }
        public string? Secret { get; init; }
    }

    private sealed record RoleRepresentation
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
    }

    private sealed record UserRepresentation
    {
        public string? Id { get; init; }
    }
}
