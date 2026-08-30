using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;
using AethericForge.Runtime.Models.Identity.Directory;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AethericForge.Runtime.Providers.Identity.Keycloak;

/// <summary>Reads identities and direct group membership from the Keycloak Admin REST API.</summary>
public sealed class KeycloakExternalIdentityDirectory : IExternalIdentityDirectory, IDisposable
{
    private const int PageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly Uri _tokenEndpoint;
    private readonly Uri _adminRealmEndpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _freshnessLifetime;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAtUtc;

    public KeycloakExternalIdentityDirectory(
        HttpClient httpClient,
        KeycloakOptions options,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(options);

        var authority = RequiredAbsoluteUri(options.Authority, nameof(options.Authority));
        _clientId = Required(options.ClientId, nameof(options.ClientId));
        _clientSecret = Required(options.ClientSecret, nameof(options.ClientSecret));
        Realm = Required(options.Realm, nameof(options.Realm));
        _freshnessLifetime = options.DirectoryFreshnessLifetime;
        if (_freshnessLifetime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.DirectoryFreshnessLifetime),
                _freshnessLifetime,
                "Directory freshness lifetime cannot be negative.");
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _tokenEndpoint = new Uri(EnsureTrailingSlash(authority), "protocol/openid-connect/token");
        var adminBase = string.IsNullOrWhiteSpace(options.AdminApiBaseAddress)
            ? DeriveAdminApiBaseAddress(authority)
            : RequiredAbsoluteUri(options.AdminApiBaseAddress, nameof(options.AdminApiBaseAddress));
        _adminRealmEndpoint = new Uri(
            EnsureTrailingSlash(adminBase),
            $"realms/{Uri.EscapeDataString(Realm)}/");
    }

    public string Provider => "Keycloak";
    public string Realm { get; }

    public Task<IExternalDirectoryResult<IExternalIdentity>> GetIdentityAsync(
        IExternalIdentityReference identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        if (!BelongsToDirectory(identity.Provider, identity.Realm))
        {
            return Task.FromResult<IExternalDirectoryResult<IExternalIdentity>>(
                Untrusted<IExternalIdentity>("The identity reference belongs to another provider or realm."));
        }

        return GetIdentityCoreAsync(identity.SubjectId, cancellationToken);
    }

    public async Task<IExternalDirectoryResult<IReadOnlyCollection<IExternalGroupReference>>> GetGroupsAsync(
        IExternalIdentityReference identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        if (!BelongsToDirectory(identity.Provider, identity.Realm))
        {
            return Untrusted<IReadOnlyCollection<IExternalGroupReference>>(
                "The identity reference belongs to another provider or realm.");
        }

        var path = $"users/{Escape(identity.SubjectId)}/groups?briefRepresentation=true";
        var response = await GetAllPagesAsync<GroupRepresentation>(path, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return Failure<IReadOnlyCollection<IExternalGroupReference>, List<GroupRepresentation>>(response);
        }

        IReadOnlyCollection<IExternalGroupReference> groups = response.Value!
            .Where(group => !string.IsNullOrWhiteSpace(group.Id))
            .Select(group => (IExternalGroupReference)new ExternalGroupReference(Provider, Realm, group.Id!))
            .OrderBy(group => group.GroupId, StringComparer.Ordinal)
            .ToArray();
        return Success(groups);
    }

    public async Task<IExternalDirectoryResult<IReadOnlyCollection<IExternalIdentity>>> GetGroupMembersAsync(
        IExternalGroupReference group,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        cancellationToken.ThrowIfCancellationRequested();
        if (!BelongsToDirectory(group.Provider, group.Realm))
        {
            return Untrusted<IReadOnlyCollection<IExternalIdentity>>(
                "The group reference belongs to another provider or realm.");
        }

        var path = $"groups/{Escape(group.GroupId)}/members?briefRepresentation=false";
        var response = await GetAllPagesAsync<UserRepresentation>(path, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return Failure<IReadOnlyCollection<IExternalIdentity>, List<UserRepresentation>>(response);
        }

        IReadOnlyCollection<IExternalIdentity> identities = response.Value!
            .Where(user => !string.IsNullOrWhiteSpace(user.Id))
            .Select(ToExternalIdentity)
            .OrderBy(identity => identity.Reference.SubjectId, StringComparer.Ordinal)
            .ToArray();
        return Success(identities);
    }

    public void Dispose()
    {
        _tokenLock.Dispose();
    }

    private async Task<IExternalDirectoryResult<IExternalIdentity>> GetIdentityCoreAsync(
        string subjectId,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<UserRepresentation>(
            $"users/{Escape(subjectId)}",
            cancellationToken).ConfigureAwait(false);
        return response.IsSuccess
            ? Success<IExternalIdentity>(ToExternalIdentity(response.Value!))
            : Failure<IExternalIdentity, UserRepresentation>(response);
    }

    private async Task<ApiResult<T>> GetAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        string token;
        try
        {
            token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (KeycloakDirectoryException exception)
        {
            return ApiResult<T>.Failure(exception.Status, exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return ApiResult<T>.Failure(ExternalDirectoryStatus.Unavailable, exception.Message);
        }
        catch (JsonException exception)
        {
            return ApiResult<T>.Failure(ExternalDirectoryStatus.Misconfigured, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return ApiResult<T>.Failure(ExternalDirectoryStatus.Misconfigured, exception.Message);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_adminRealmEndpoint, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<T>.Failure(MapStatus(response.StatusCode), await FailureReasonAsync(response, cancellationToken));
            }

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return value is null
                ? ApiResult<T>.Failure(ExternalDirectoryStatus.Misconfigured, "Keycloak returned an empty response.")
                : ApiResult<T>.Success(value);
        }
        catch (HttpRequestException exception)
        {
            return ApiResult<T>.Failure(ExternalDirectoryStatus.Unavailable, exception.Message);
        }
        catch (JsonException exception)
        {
            return ApiResult<T>.Failure(ExternalDirectoryStatus.Misconfigured, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return ApiResult<T>.Failure(ExternalDirectoryStatus.Misconfigured, exception.Message);
        }
    }

    private async Task<ApiResult<List<T>>> GetAllPagesAsync<T>(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var values = new List<T>();
        for (var first = 0; ; first += PageSize)
        {
            var page = await GetAsync<List<T>>(
                $"{relativePath}&first={first}&max={PageSize}",
                cancellationToken).ConfigureAwait(false);
            if (!page.IsSuccess)
            {
                return page;
            }

            values.AddRange(page.Value!);
            if (page.Value!.Count < PageSize)
            {
                return ApiResult<List<T>>.Success(values);
            }
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
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
                throw new KeycloakDirectoryException(
                    MapTokenStatus(response.StatusCode),
                    await FailureReasonAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
            {
                throw new KeycloakDirectoryException(
                    ExternalDirectoryStatus.Misconfigured,
                    "Keycloak did not return an access token.");
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

    private ExternalIdentity ToExternalIdentity(UserRepresentation user)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddProperty(properties, "username", user.Username);
        AddProperty(properties, "email", user.Email);
        AddProperty(properties, "firstName", user.FirstName);
        AddProperty(properties, "lastName", user.LastName);
        if (user.Attributes is not null)
        {
            foreach (var attribute in user.Attributes.OrderBy(attribute => attribute.Key, StringComparer.Ordinal))
            {
                var value = attribute.Value?.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
                AddProperty(properties, attribute.Key, value);
            }
        }

        var displayName = string.Join(' ', new[] { user.FirstName, user.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = user.Username ?? user.Email;
        }

        return new ExternalIdentity(
            new ExternalIdentityReference(Provider, Realm, user.Id!),
            displayName,
            user.Enabled,
            properties);
    }

    private static void AddProperty(IDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !properties.ContainsKey(key))
        {
            properties.Add(key, value);
        }
    }

    private ExternalDirectoryResult<T> Success<T>(T value)
    {
        var observedAt = Now();
        return ExternalDirectoryResult<T>.Success(value, observedAt, observedAt.Add(_freshnessLifetime));
    }

    private ExternalDirectoryResult<T> Failure<T, TValue>(ApiResult<TValue> result) =>
        ExternalDirectoryResult<T>.Failure(result.Status, Now(), result.FailureReason);

    private ExternalDirectoryResult<T> Untrusted<T>(string reason) =>
        ExternalDirectoryResult<T>.Failure(ExternalDirectoryStatus.Untrusted, Now(), reason);

    private bool BelongsToDirectory(string provider, string realm) =>
        string.Equals(provider, Provider, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(realm, Realm, StringComparison.OrdinalIgnoreCase);

    private DateTimeOffset Now() => _timeProvider.GetUtcNow();
    private static string Escape(string value) => Uri.EscapeDataString(value);
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

    private static Uri DeriveAdminApiBaseAddress(Uri authority)
    {
        var marker = "/realms/";
        var index = authority.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            throw new ArgumentException(
                "Authority must contain '/realms/' when AdminApiBaseAddress is not supplied.",
                nameof(KeycloakOptions.Authority));
        }
        var builder = new UriBuilder(authority) { Path = authority.AbsolutePath[..index] + "/admin/", Query = "", Fragment = "" };
        return builder.Uri;
    }

    private static ExternalDirectoryStatus MapStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => ExternalDirectoryStatus.NotFound,
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ExternalDirectoryStatus.Untrusted,
        HttpStatusCode.BadRequest => ExternalDirectoryStatus.Misconfigured,
        _ => ExternalDirectoryStatus.Unavailable
    };

    private static ExternalDirectoryStatus MapTokenStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ExternalDirectoryStatus.Misconfigured,
        _ => ExternalDirectoryStatus.Unavailable
    };

    private static async Task<string?> FailureReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var reason = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        reason = string.IsNullOrWhiteSpace(reason) ? response.ReasonPhrase : reason.Trim();
        return reason is { Length: > 2048 } ? reason[..2048] : reason;
    }

    private sealed record ApiResult<T>(bool IsSuccess, T? Value, ExternalDirectoryStatus Status, string? FailureReason)
    {
        public static ApiResult<T> Success(T value) => new(true, value, ExternalDirectoryStatus.Success, null);
        public static ApiResult<T> Failure(ExternalDirectoryStatus status, string? reason) => new(false, default, status, reason);
    }

    private sealed class KeycloakDirectoryException(ExternalDirectoryStatus status, string? message)
        : Exception(message)
    {
        public ExternalDirectoryStatus Status { get; } = status;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    }

    private sealed class UserRepresentation
    {
        public string? Id { get; init; }
        public string? Username { get; init; }
        public string? Email { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public bool Enabled { get; init; }
        public Dictionary<string, string[]?>? Attributes { get; init; }
    }

    private sealed class GroupRepresentation
    {
        public string? Id { get; init; }
    }
}
