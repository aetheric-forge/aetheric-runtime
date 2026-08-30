namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;

public interface IExternalIdentityDirectory
{
    string Provider { get; }
    string Realm { get; }

    Task<IExternalDirectoryResult<IExternalIdentity>> GetIdentityAsync(
        IExternalIdentityReference identity,
        CancellationToken cancellationToken = default);

    Task<IExternalDirectoryResult<IReadOnlyCollection<IExternalGroupReference>>> GetGroupsAsync(
        IExternalIdentityReference identity,
        CancellationToken cancellationToken = default);

    Task<IExternalDirectoryResult<IReadOnlyCollection<IExternalIdentity>>> GetGroupMembersAsync(
        IExternalGroupReference group,
        CancellationToken cancellationToken = default);
}
