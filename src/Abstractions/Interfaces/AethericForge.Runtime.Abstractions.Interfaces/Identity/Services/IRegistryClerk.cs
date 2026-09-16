using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Clients;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;

/// <summary>
/// The write-side counterpart to <see cref="IRegistrar"/> - registers clients and manages the
/// roles, groups, and permission assignments an identity provider (e.g. Keycloak) uses to
/// authorize principals. A Clerk acts with its own standing administrative credentials; it is
/// not scoped to, or aware of, any particular calling principal.
/// </summary>
public interface IRegistryClerk
{
    Task<IRegistryOperationResult<IClientRegistration>> RegisterClientAsync(
        ClientRegistrationRequest request,
        CancellationToken ct = default);

    /// <summary>Replaces the settings of an existing client. Never changes its secret.</summary>
    Task<IRegistryOperationResult<IClientRegistration>> UpdateClientAsync(
        string clientId,
        ClientRegistrationRequest request,
        CancellationToken ct = default);

    Task<IRegistryOperationResult> DeleteClientAsync(
        string clientId,
        CancellationToken ct = default);

    Task<IRegistryOperationResult<IRole>> CreateRoleAsync(
        string name,
        string? description = null,
        CancellationToken ct = default);

    Task<IRegistryOperationResult> DeleteRoleAsync(
        string name,
        CancellationToken ct = default);

    /// <param name="parentPath">
    /// The full path of the parent group (e.g. <c>/teams</c>), or <see langword="null"/> to
    /// create a top-level group.
    /// </param>
    Task<IRegistryOperationResult<IGroup>> CreateGroupAsync(
        string name,
        string? parentPath = null,
        CancellationToken ct = default);

    /// <param name="path">The group's full path, as returned on <see cref="IGroup.Path"/>.</param>
    Task<IRegistryOperationResult> DeleteGroupAsync(
        string path,
        CancellationToken ct = default);

    Task<IRegistryOperationResult> AssignRoleToGroupAsync(
        string roleName,
        string groupPath,
        CancellationToken ct = default);

    Task<IRegistryOperationResult> AssignRoleToPrincipalAsync(
        string roleName,
        string subjectId,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures each permission exists as its own atomic role (auto-created if missing), then
    /// adds it as a composite of <paramref name="roleName"/> - so anything holding
    /// <paramref name="roleName"/> transitively holds every permission assigned to it. A
    /// permission's role name is derived from its <see cref="IPermission.Scope"/>,
    /// <see cref="IPermission.Resource"/>, and <see cref="IPermission.Action"/>.
    /// </summary>
    Task<IRegistryOperationResult> AssignPermissionsToRoleAsync(
        string roleName,
        IReadOnlyCollection<IPermission> permissions,
        CancellationToken ct = default);
}
