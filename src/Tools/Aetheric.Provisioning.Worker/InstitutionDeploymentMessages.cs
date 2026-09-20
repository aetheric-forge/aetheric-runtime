using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Models.Post;

namespace Aetheric.Provisioning.Worker;

/// <summary>
/// Independently defined here, matching aetheric-admin's copy shape-for-shape - Post's RabbitMQ
/// wire format is JSON-structural (matched by Domain/Address/Contract.Name/Version), not a
/// shared compiled type, so the two repos don't need a shared contracts package for this.
///
/// Deploys an arbitrary institution (University, Campus, or anything else this Worker's
/// definition source can resolve) - not just Campus, despite this type's history. Renamed from
/// CampusDeploymentRequested/Completed (contract bumped 1.0 -> 2.0, address renamed) now that a
/// University deployment routes through the exact same message, and Parent below lets a non-root
/// institution (Campus, now a child of University) carry its real parent identity instead of the
/// self-reference every deployment used before University existed.
/// </summary>
public sealed record InstitutionDeploymentRequested(
    Guid RequestId,
    string Repository,
    string Revision,
    string DefinitionPath,
    string BindingsPath,
    IReadOnlyDictionary<string, RootCredentialPayload> RootCredentials,
    ParentIdentity? Parent,
    DateTimeOffset RequestedAtUtc);

public sealed record RootCredentialPayload(
    string Host,
    int Port,
    string? Username,
    string Password,
    string? AuthDatabase = null,
    string? Database = null,
    string? Scheme = null,
    string? BasePath = null,
    string? Realm = null);

/// <summary>
/// The deploying institution's real parent identity - null for a root institution (e.g.
/// University itself), non-null for anything with a real "dependencies" block in its own
/// definition (e.g. Campus's IRegistrar dependency on University). Repository/Revision become
/// the plan's ParentContext identity.
///
/// ResourceLocations carries whatever each declared contract's own live-verifying resolver needs
/// to perform its check - keyed by contract name ("IRegistrar"/"IArchive"/"IPostOffice" -> the
/// realm/bucket/vhost that actually owns it), the deploying operator already knows these, since
/// they triggered that ancestor's own deployment first. "ILibrary" is the one exception: Mongo
/// creates databases lazily on write and this provisioner never writes data, so "does the
/// database exist" isn't a reliable signal - the live check instead verifies the scoped user the
/// owning level's own MongoDbResourceProvider actually created, which needs BOTH the database
/// name and which institution owns it. Encoded as a single "{database}@{owningInstitutionId}"
/// string rather than a second parallel dictionary just for one contract's extra field - neither
/// a Mongo database name nor an institution id slug can contain "@".
/// </summary>
public sealed record ParentIdentity(string Repository, string Revision, IReadOnlyDictionary<string, string> ResourceLocations);

public sealed record InstitutionDeploymentCompleted(
    Guid RequestId,
    bool Succeeded,
    IReadOnlyList<string> Issues,
    DateTimeOffset CompletedAtUtc);

public static class ProvisioningPost
{
    public const string Domain = "provisioning";

    public static IPostReference RequestReference() => new PostReference(
        Domain,
        "institution/deploy",
        new PostContract("institution-deployment-requested", "2.0", PostIntent.Command));

    public static IPostReference ResultReference() => new PostReference(
        Domain,
        "institution/deploy/result",
        new PostContract("institution-deployment-completed", "2.0", PostIntent.Event));
}
