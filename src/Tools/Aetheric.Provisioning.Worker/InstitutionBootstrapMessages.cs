using System.Collections.Immutable;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Models.Post;

namespace Aetheric.Provisioning.Worker;

/// <summary>
/// Bootstraps a whole new University hierarchy - University, its one Campus, an Administration
/// Faculty, and a Decisions Institution - in one operator action, rather than four separate
/// InstitutionDeploymentRequested messages. Independently defined here, matching the org's
/// established "no shared contracts package" convention (see InstitutionDeploymentMessages.cs).
///
/// Every one of the four configs is the same InstitutionConfig shape (raw definition+bindings
/// YAML text) - University, Campus, Administration Faculty, and Decisions are all deployed
/// through the identical load/configure/review/approve/execute sequence
/// (InstitutionDeploymentRequestConsumer.DeployOneAsync); none is special-cased. Each level's own
/// submitted bindings decide whether it privatizes a resource (ownership: owned) or inherits it
/// from the nearest ancestor that owns it (ownership: parent) - see
/// InstitutionBootstrapRequestConsumer's own doc comment for how that resolution works.
/// </summary>
public sealed record InstitutionBootstrapRequested(
    Guid RequestId,
    InstitutionConfig University,
    InstitutionConfig Campus,
    InstitutionConfig AdministrationFaculty,
    InstitutionConfig Decisions,
    IReadOnlyDictionary<string, RootCredentialPayload> RootCredentials,
    DateTimeOffset RequestedAtUtc);

/// <summary>
/// Raw institution.yaml/institution.bindings.yaml-shaped text, submitted inline - the form IS the
/// content, not a GitHub commit reference. Turned into a SourceBundle with synthetic,
/// obviously-non-GitHub provenance by InlineSourceDocuments.Build.
/// </summary>
public sealed record InstitutionConfig(string DefinitionYaml, string BindingsYaml);

public enum BootstrapStepStatus { Succeeded, Failed, NotAttempted }

public sealed record BootstrapStepResult(string Step, BootstrapStepStatus Status, IReadOnlyList<string> Issues);

public sealed record InstitutionBootstrapCompleted(
    Guid RequestId,
    bool Succeeded,
    ImmutableArray<BootstrapStepResult> Steps,
    DateTimeOffset CompletedAtUtc);

public static class ProvisioningBootstrapPost
{
    public static IPostReference RequestReference() => new PostReference(
        ProvisioningPost.Domain,
        "institution/deploy/bootstrap",
        new PostContract("institution-bootstrap-requested", "1.0", PostIntent.Command));

    public static IPostReference ResultReference() => new PostReference(
        ProvisioningPost.Domain,
        "institution/deploy/bootstrap/result",
        new PostContract("institution-bootstrap-completed", "1.0", PostIntent.Event));
}
