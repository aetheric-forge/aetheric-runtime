using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Clients;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;
using AethericForge.Runtime.Models.Identity.Authorization;
using AethericForge.Runtime.Models.Identity.Clients;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AethericForge.Runtime.Providers.Identity.Keycloak;

/// <summary>
/// Writes clients, roles, groups, and role/permission assignments through the Keycloak Admin
/// REST API. The read/query counterpart is <see cref="KeycloakExternalIdentityDirectory"/> -
/// both share <see cref="KeycloakAdminAccess"/> for service-account token acquisition.
///
/// Permissions have no first-class representation in Keycloak short of its heavyweight UMA
/// authorization-services feature (per-client resources/scopes/policies), which is far more
/// than "assign this permission to that role" needs. Instead each <see cref="IPermission"/> is
/// represented as its own atomic realm role (auto-created on first use, named by
/// <see cref="PermissionRoleName"/>), and "assigning" it to a role makes it one of that role's
/// composites - Keycloak already resolves composite roles transitively everywhere role
/// membership is checked, so nothing else has to know permissions aren't "real" roles.
/// </summary>
public sealed class KeycloakRegistryClerk : IRegistryClerk, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly KeycloakAdminAccess _access;

    public KeycloakRegistryClerk(HttpClient httpClient, KeycloakOptions options, TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _access = new KeycloakAdminAccess(httpClient, options, timeProvider);
    }

    public void Dispose() => _access.Dispose();

    public Task<IRegistryOperationResult<IClientRegistration>> GetClientAsync(
        string clientId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        return LoadClientAsync(clientId, includeSecret: false, ct);
    }

    public async Task<IRegistryOperationResult<IRole>> GetRoleAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var role = await FindRoleRepresentationAsync(name, ct).ConfigureAwait(false);
        return role.IsSuccess
            ? RegistryOperationResult<IRole>.Succeeded(new Role(role.Value!.Name!))
            : Failure<IRole>(role.Status, role.FailureReason!);
    }

    public async Task<IRegistryOperationResult<IGroup>> GetGroupAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var group = await FindGroupRepresentationByPathAsync(path, ct).ConfigureAwait(false);
        return group.IsSuccess
            ? RegistryOperationResult<IGroup>.Succeeded(new Group(group.Value!.Name!, group.Value.Path!))
            : Failure<IGroup>(group.Status, group.FailureReason!);
    }

    public async Task<IRegistryOperationResult<IClientRegistration>> RegisterClientAsync(
        ClientRegistrationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var representation = ToRepresentation(request, enabled: true);
        var created = await PostAsync("clients", representation, ct).ConfigureAwait(false);
        if (!created.IsSuccess)
        {
            return Failure<IClientRegistration>(created.Status, created.FailureReason!);
        }

        return await LoadClientAsync(request.ClientId, includeSecret: true, ct).ConfigureAwait(false);
    }

    public async Task<IRegistryOperationResult<IClientRegistration>> UpdateClientAsync(
        string clientId,
        ClientRegistrationRequest request,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(request);

        var existing = await FindClientRepresentationAsync(clientId, ct).ConfigureAwait(false);
        if (!existing.IsSuccess)
        {
            return Failure<IClientRegistration>(existing.Status, existing.FailureReason!);
        }

        var updated = ToRepresentation(request, enabled: true) with { Id = existing.Value!.Id };
        var put = await PutAsync($"clients/{Escape(existing.Value.Id!)}", updated, ct).ConfigureAwait(false);
        if (!put.IsSuccess)
        {
            return Failure<IClientRegistration>(put.Status, put.FailureReason!);
        }

        return await LoadClientAsync(request.ClientId, includeSecret: false, ct).ConfigureAwait(false);
    }

    public async Task<IRegistryOperationResult> DeleteClientAsync(string clientId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var existing = await FindClientRepresentationAsync(clientId, ct).ConfigureAwait(false);
        if (!existing.IsSuccess)
        {
            return Failure(existing.Status, existing.FailureReason!);
        }

        var deleted = await DeleteAsync($"clients/{Escape(existing.Value!.Id!)}", ct).ConfigureAwait(false);
        return deleted.IsSuccess ? Succeeded() : Failure(deleted.Status, deleted.FailureReason!);
    }

    public async Task<IRegistryOperationResult<IRole>> CreateRoleAsync(
        string name,
        string? description = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var created = await PostAsync("roles", new RoleRepresentation { Name = name, Description = description }, ct)
            .ConfigureAwait(false);
        if (!created.IsSuccess)
        {
            return Failure<IRole>(created.Status, created.FailureReason!);
        }

        return RegistryOperationResult<IRole>.Succeeded(new Role(name));
    }

    public async Task<IRegistryOperationResult> DeleteRoleAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var deleted = await DeleteAsync($"roles/{Escape(name)}", ct).ConfigureAwait(false);
        return deleted.IsSuccess ? Succeeded() : Failure(deleted.Status, deleted.FailureReason!);
    }

    public async Task<IRegistryOperationResult<IGroup>> CreateGroupAsync(
        string name,
        string? parentPath = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string createPath;
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            createPath = "groups";
        }
        else
        {
            var parent = await FindGroupRepresentationByPathAsync(parentPath, ct).ConfigureAwait(false);
            if (!parent.IsSuccess)
            {
                return Failure<IGroup>(parent.Status, parent.FailureReason!);
            }

            createPath = $"groups/{Escape(parent.Value!.Id!)}/children";
        }

        var created = await PostAsync(createPath, new GroupRepresentation { Name = name }, ct).ConfigureAwait(false);
        if (!created.IsSuccess)
        {
            return Failure<IGroup>(created.Status, created.FailureReason!);
        }

        var expectedPath = string.IsNullOrWhiteSpace(parentPath)
            ? $"/{name}"
            : $"{parentPath.TrimEnd('/')}/{name}";
        return RegistryOperationResult<IGroup>.Succeeded(new Group(name, expectedPath));
    }

    public async Task<IRegistryOperationResult> DeleteGroupAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var group = await FindGroupRepresentationByPathAsync(path, ct).ConfigureAwait(false);
        if (!group.IsSuccess)
        {
            return Failure(group.Status, group.FailureReason!);
        }

        var deleted = await DeleteAsync($"groups/{Escape(group.Value!.Id!)}", ct).ConfigureAwait(false);
        return deleted.IsSuccess ? Succeeded() : Failure(deleted.Status, deleted.FailureReason!);
    }

    public async Task<IRegistryOperationResult> AssignRoleToGroupAsync(
        string roleName,
        string groupPath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupPath);

        var role = await FindRoleRepresentationAsync(roleName, ct).ConfigureAwait(false);
        if (!role.IsSuccess)
        {
            return Failure(role.Status, role.FailureReason!);
        }

        var group = await FindGroupRepresentationByPathAsync(groupPath, ct).ConfigureAwait(false);
        if (!group.IsSuccess)
        {
            return Failure(group.Status, group.FailureReason!);
        }

        var assigned = await PostAsync(
            $"groups/{Escape(group.Value!.Id!)}/role-mappings/realm",
            new[] { role.Value! },
            ct).ConfigureAwait(false);
        return assigned.IsSuccess ? Succeeded() : Failure(assigned.Status, assigned.FailureReason!);
    }

    public async Task<IRegistryOperationResult> AssignRoleToPrincipalAsync(
        string roleName,
        string subjectId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var role = await FindRoleRepresentationAsync(roleName, ct).ConfigureAwait(false);
        if (!role.IsSuccess)
        {
            return Failure(role.Status, role.FailureReason!);
        }

        var assigned = await PostAsync(
            $"users/{Escape(subjectId)}/role-mappings/realm",
            new[] { role.Value! },
            ct).ConfigureAwait(false);
        return assigned.IsSuccess ? Succeeded() : Failure(assigned.Status, assigned.FailureReason!);
    }

    public async Task<IRegistryOperationResult> AssignPermissionsToRoleAsync(
        string roleName,
        IReadOnlyCollection<IPermission> permissions,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        ArgumentNullException.ThrowIfNull(permissions);
        if (permissions.Count == 0)
        {
            return Succeeded();
        }

        var role = await FindRoleRepresentationAsync(roleName, ct).ConfigureAwait(false);
        if (!role.IsSuccess)
        {
            return Failure(role.Status, role.FailureReason!);
        }

        var composites = new List<RoleRepresentation>(permissions.Count);
        foreach (var permission in permissions)
        {
            var permissionRoleName = PermissionRoleName(permission);
            var permissionRole = await FindRoleRepresentationAsync(permissionRoleName, ct).ConfigureAwait(false);
            if (permissionRole.Status == RegistryOperationStatus.NotFound)
            {
                var create = await PostAsync("roles", new RoleRepresentation { Name = permissionRoleName }, ct)
                    .ConfigureAwait(false);
                if (!create.IsSuccess)
                {
                    return Failure(create.Status, create.FailureReason!);
                }

                permissionRole = await FindRoleRepresentationAsync(permissionRoleName, ct).ConfigureAwait(false);
            }

            if (!permissionRole.IsSuccess)
            {
                return Failure(permissionRole.Status, permissionRole.FailureReason!);
            }

            composites.Add(permissionRole.Value!);
        }

        var added = await PostAsync($"roles/{Escape(roleName)}/composites", composites, ct).ConfigureAwait(false);
        return added.IsSuccess ? Succeeded() : Failure(added.Status, added.FailureReason!);
    }

    /// <summary>The canonical <c>scope:resource:action</c> (or <c>scope:action</c> with no resource) role name a permission is represented as.</summary>
    private static string PermissionRoleName(IPermission permission) =>
        permission.Resource is null
            ? $"{permission.Scope}:{permission.Action}"
            : $"{permission.Scope}:{permission.Resource}:{permission.Action}";

    private async Task<ApiResult<ClientRepresentation>> FindClientRepresentationAsync(
        string clientId,
        CancellationToken ct)
    {
        var matches = await GetAsync<List<ClientRepresentation>>(
            $"clients?clientId={Escape(clientId)}&exact=true",
            ct).ConfigureAwait(false);
        if (!matches.IsSuccess)
        {
            return ApiResult<ClientRepresentation>.Failure(matches.Status, matches.FailureReason);
        }

        var match = matches.Value!.FirstOrDefault(client => string.Equals(client.ClientId, clientId, StringComparison.Ordinal));
        return match is null
            ? ApiResult<ClientRepresentation>.Failure(RegistryOperationStatus.NotFound, $"Keycloak client '{clientId}' was not found.")
            : ApiResult<ClientRepresentation>.Success(match);
    }

    private async Task<IRegistryOperationResult<IClientRegistration>> LoadClientAsync(
        string clientId,
        bool includeSecret,
        CancellationToken ct)
    {
        var existing = await FindClientRepresentationAsync(clientId, ct).ConfigureAwait(false);
        if (!existing.IsSuccess)
        {
            return Failure<IClientRegistration>(existing.Status, existing.FailureReason!);
        }

        string? secret = null;
        if (includeSecret && existing.Value!.PublicClient != true)
        {
            var secretResult = await GetAsync<ClientSecretRepresentation>(
                $"clients/{Escape(existing.Value.Id!)}/client-secret",
                ct).ConfigureAwait(false);
            secret = secretResult.IsSuccess ? secretResult.Value!.Value : null;
        }

        var representation = existing.Value!;
        var registration = new ClientRegistration(
            representation.ClientId!,
            representation.Name,
            representation.Enabled ?? true,
            representation.PublicClient ?? false,
            representation.StandardFlowEnabled ?? true,
            representation.DirectAccessGrantsEnabled ?? false,
            representation.RedirectUris ?? [],
            representation.WebOrigins ?? [],
            secret);
        return RegistryOperationResult<IClientRegistration>.Succeeded(registration);
    }

    private async Task<ApiResult<RoleRepresentation>> FindRoleRepresentationAsync(string name, CancellationToken ct)
    {
        var role = await GetAsync<RoleRepresentation>($"roles/{Escape(name)}", ct).ConfigureAwait(false);
        return role.IsSuccess
            ? ApiResult<RoleRepresentation>.Success(role.Value!)
            : ApiResult<RoleRepresentation>.Failure(role.Status, role.FailureReason);
    }

    /// <summary>
    /// Keycloak's group search matches by name, not path - an exact-name match can still be
    /// ambiguous (two groups of the same name under different parents), so this walks the
    /// realm's group tree and matches the caller's path exactly, the same defensive approach
    /// <see cref="KeycloakExternalIdentityDirectory.ResolveGroupAsync"/> takes for names.
    /// </summary>
    private async Task<ApiResult<GroupRepresentation>> FindGroupRepresentationByPathAsync(string path, CancellationToken ct)
    {
        var normalized = path.StartsWith('/') ? path : $"/{path}";
        var all = await GetAsync<List<GroupRepresentation>>("groups?populateHierarchy=true", ct).ConfigureAwait(false);
        if (!all.IsSuccess)
        {
            return ApiResult<GroupRepresentation>.Failure(all.Status, all.FailureReason);
        }

        var match = Flatten(all.Value!).FirstOrDefault(group => string.Equals(group.Path, normalized, StringComparison.Ordinal));
        return match is null
            ? ApiResult<GroupRepresentation>.Failure(RegistryOperationStatus.NotFound, $"Keycloak group '{normalized}' was not found.")
            : ApiResult<GroupRepresentation>.Success(match);
    }

    private static IEnumerable<GroupRepresentation> Flatten(IEnumerable<GroupRepresentation> groups)
    {
        foreach (var group in groups)
        {
            yield return group;
            foreach (var subgroup in Flatten(group.SubGroups ?? []))
            {
                yield return subgroup;
            }
        }
    }

    private static ClientRepresentation ToRepresentation(ClientRegistrationRequest request, bool enabled) => new()
    {
        ClientId = request.ClientId,
        Name = request.DisplayName,
        Enabled = enabled,
        PublicClient = request.PublicClient,
        StandardFlowEnabled = request.StandardFlowEnabled,
        DirectAccessGrantsEnabled = request.DirectAccessGrantsEnabled,
        ServiceAccountsEnabled = !request.PublicClient,
        RedirectUris = request.RedirectUris?.ToList() ?? [],
        WebOrigins = request.WebOrigins?.ToList() ?? [],
        Protocol = "openid-connect",
    };

    private async Task<ApiResult<T>> GetAsync<T>(string relativePath, CancellationToken ct)
    {
        var token = await TryGetAccessTokenAsync<T>(ct).ConfigureAwait(false);
        if (token.Failure is not null)
        {
            return token.Failure;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_access.AdminRealmEndpoint, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await SendAsync<T>(request, ct).ConfigureAwait(false);
    }

    private async Task<ApiResult<Unit>> PostAsync<TBody>(string relativePath, TBody body, CancellationToken ct)
    {
        var token = await TryGetAccessTokenAsync<Unit>(ct).ConfigureAwait(false);
        if (token.Failure is not null)
        {
            return token.Failure;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_access.AdminRealmEndpoint, relativePath))
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await SendNoContentAsync(request, ct).ConfigureAwait(false);
    }

    private async Task<ApiResult<Unit>> PutAsync<TBody>(string relativePath, TBody body, CancellationToken ct)
    {
        var token = await TryGetAccessTokenAsync<Unit>(ct).ConfigureAwait(false);
        if (token.Failure is not null)
        {
            return token.Failure;
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(_access.AdminRealmEndpoint, relativePath))
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await SendNoContentAsync(request, ct).ConfigureAwait(false);
    }

    private async Task<ApiResult<Unit>> DeleteAsync(string relativePath, CancellationToken ct)
    {
        var token = await TryGetAccessTokenAsync<Unit>(ct).ConfigureAwait(false);
        if (token.Failure is not null)
        {
            return token.Failure;
        }

        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(_access.AdminRealmEndpoint, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await SendNoContentAsync(request, ct).ConfigureAwait(false);
    }

    private async Task<(string Token, ApiResult<T>? Failure)> TryGetAccessTokenAsync<T>(CancellationToken ct)
    {
        try
        {
            return (await _access.GetAccessTokenAsync(ct).ConfigureAwait(false), null);
        }
        catch (KeycloakAdminAccessException exception)
        {
            return (string.Empty, ApiResult<T>.Failure(MapTokenStatus(exception.StatusCode), exception.Message));
        }
        catch (HttpRequestException exception)
        {
            return (string.Empty, ApiResult<T>.Failure(RegistryOperationStatus.Unavailable, exception.Message));
        }
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<T>.Failure(
                    MapStatus(response.StatusCode),
                    await KeycloakAdminAccess.FailureReasonAsync(response, ct).ConfigureAwait(false));
            }

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
            return value is null
                ? ApiResult<T>.Failure(RegistryOperationStatus.Unavailable, "Keycloak returned an empty response.")
                : ApiResult<T>.Success(value);
        }
        catch (HttpRequestException exception)
        {
            return ApiResult<T>.Failure(RegistryOperationStatus.Unavailable, exception.Message);
        }
        catch (JsonException exception)
        {
            return ApiResult<T>.Failure(RegistryOperationStatus.Unavailable, exception.Message);
        }
    }

    /// <summary>For POST/PUT/DELETE calls whose success response is empty (Keycloak returns 201/204 with no body).</summary>
    private async Task<ApiResult<Unit>> SendNoContentAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? ApiResult<Unit>.Success(Unit.Value)
                : ApiResult<Unit>.Failure(
                    MapStatus(response.StatusCode),
                    await KeycloakAdminAccess.FailureReasonAsync(response, ct).ConfigureAwait(false));
        }
        catch (HttpRequestException exception)
        {
            return ApiResult<Unit>.Failure(RegistryOperationStatus.Unavailable, exception.Message);
        }
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static RegistryOperationStatus MapStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Conflict => RegistryOperationStatus.AlreadyExists,
        HttpStatusCode.NotFound => RegistryOperationStatus.NotFound,
        HttpStatusCode.BadRequest => RegistryOperationStatus.Invalid,
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => RegistryOperationStatus.Unauthorized,
        _ => RegistryOperationStatus.Unavailable
    };

    private static RegistryOperationStatus MapTokenStatus(HttpStatusCode? statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => RegistryOperationStatus.Unauthorized,
        _ => RegistryOperationStatus.Unavailable
    };

    private static IRegistryOperationResult<TValue> Failure<TValue>(RegistryOperationStatus status, string reason) =>
        RegistryOperationResult<TValue>.Failure(status, reason);

    private static IRegistryOperationResult Failure(RegistryOperationStatus status, string reason) =>
        RegistryOperationResult.Failure(status, reason);

    private static IRegistryOperationResult Succeeded() => RegistryOperationResult.Succeeded();

    private readonly record struct Unit
    {
        public static readonly Unit Value = default;
    }

    private sealed record ApiResult<T>(bool IsSuccess, T? Value, RegistryOperationStatus Status, string? FailureReason)
    {
        public static ApiResult<T> Success(T value) => new(true, value, RegistryOperationStatus.Succeeded, null);
        public static ApiResult<T> Failure(RegistryOperationStatus status, string? reason) => new(false, default, status, reason);
    }

    private sealed record ClientRepresentation
    {
        public string? Id { get; init; }
        public string? ClientId { get; init; }
        public string? Name { get; init; }
        public bool? Enabled { get; init; }
        public bool? PublicClient { get; init; }
        public bool? StandardFlowEnabled { get; init; }
        public bool? DirectAccessGrantsEnabled { get; init; }
        public bool? ServiceAccountsEnabled { get; init; }
        public List<string>? RedirectUris { get; init; }
        public List<string>? WebOrigins { get; init; }
        public string? Protocol { get; init; }
    }

    private sealed record ClientSecretRepresentation
    {
        public string? Type { get; init; }
        public string? Value { get; init; }
    }

    private sealed record RoleRepresentation
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public bool? Composite { get; init; }
    }

    private sealed record GroupRepresentation
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Path { get; init; }
        public List<GroupRepresentation>? SubGroups { get; init; }
    }
}
