using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;
using AethericForge.Runtime.Models.Identity.Directory;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AethericForge.Runtime.Providers.Identity.Keycloak;

/// <summary>Reads identities and direct group membership from the Keycloak Admin REST API.</summary>
public sealed class KeycloakExternalIdentityDirectory : IExternalIdentityDirectory, IDisposable
{
    private const int PageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly KeycloakAdminAccess _access;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _freshnessLifetime;

    public KeycloakExternalIdentityDirectory(
        HttpClient httpClient,
        KeycloakOptions options,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(options);

        _freshnessLifetime = options.DirectoryFreshnessLifetime;
        if (_freshnessLifetime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.DirectoryFreshnessLifetime),
                _freshnessLifetime,
                "Directory freshness lifetime cannot be negative.");
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _access = new KeycloakAdminAccess(httpClient, options, _timeProvider);
        Realm = _access.Realm;
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

    /// <summary>Resolves one unambiguous Keycloak group by its exact display name.</summary>
    public async Task<IExternalDirectoryResult<IExternalGroupReference>> ResolveGroupAsync(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new ArgumentException("A group name is required.", nameof(groupName));
        }
        cancellationToken.ThrowIfCancellationRequested();

        var encodedName = Uri.EscapeDataString(groupName.Trim());
        var response = await GetAllPagesAsync<GroupRepresentation>(
            $"groups?briefRepresentation=true&populateHierarchy=true&exact=true&search={encodedName}",
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return Failure<IExternalGroupReference, List<GroupRepresentation>>(response);
        }

        var matches = FlattenGroups(response.Value!)
            .Where(group =>
                !string.IsNullOrWhiteSpace(group.Id) &&
                string.Equals(group.Name, groupName.Trim(), StringComparison.Ordinal))
            .GroupBy(group => group.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return matches.Length switch
        {
            0 => ExternalDirectoryResult<IExternalGroupReference>.Failure(
                ExternalDirectoryStatus.NotFound,
                Now(),
                $"Keycloak group '{groupName.Trim()}' was not found."),
            1 => Success<IExternalGroupReference>(
                new ExternalGroupReference(Provider, Realm, matches[0].Id!)),
            _ => ExternalDirectoryResult<IExternalGroupReference>.Failure(
                ExternalDirectoryStatus.Misconfigured,
                Now(),
                $"More than one Keycloak group is named '{groupName.Trim()}'. Configure its group ID instead.")
        };
    }

    public void Dispose()
    {
        _access.Dispose();
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
            token = await _access.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (KeycloakAdminAccessException exception)
        {
            return ApiResult<T>.Failure(MapTokenStatus(exception.StatusCode), exception.Message);
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

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_access.AdminRealmEndpoint, relativePath));
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

    private static IEnumerable<GroupRepresentation> FlattenGroups(IEnumerable<GroupRepresentation> groups)
    {
        foreach (var group in groups)
        {
            yield return group;
            foreach (var subgroup in FlattenGroups(group.SubGroups ?? []))
            {
                yield return subgroup;
            }
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

    private static ExternalDirectoryStatus MapStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => ExternalDirectoryStatus.NotFound,
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ExternalDirectoryStatus.Untrusted,
        HttpStatusCode.BadRequest => ExternalDirectoryStatus.Misconfigured,
        _ => ExternalDirectoryStatus.Unavailable
    };

    private static ExternalDirectoryStatus MapTokenStatus(HttpStatusCode? statusCode) => statusCode switch
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
        public string? Name { get; init; }
        public List<GroupRepresentation>? SubGroups { get; init; }
    }
}
