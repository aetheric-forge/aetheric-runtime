using System.Collections.Immutable;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.Application;

public static class InstitutionDeploymentRecorder
{
    public static InstitutionDeploymentAttempt Begin(LoadedInstitution loaded, ProvisioningPlan plan,
        Guid requestId, DateTimeOffset requestedAtUtc, DeploymentParent? parent) => new(
        InstitutionDeploymentAttempt.Identity(loaded.Source.Definition.Provenance.Repository,
            loaded.Requirements.Id, plan.Input.Bindings.Environment), requestId,
        loaded.Requirements.Id, loaded.Name, loaded.Description, loaded.Requirements.Version,
        plan.Input.Bindings.Environment, parent, loaded.Source.Definition.Provenance,
        loaded.Source.Bindings.Provenance, plan.Id, InstitutionDeploymentStatus.Pending, requestedAtUtc, null,
        plan.Input.Bindings.Resources.ToImmutableDictionary(pair => pair.Key, pair => new ProvisionedResource(
            pair.Key, loaded.Requirements.Resources.Single(r => r.Id == pair.Key).Category,
            pair.Value.Provider, pair.Value.Settings, [])), [], []);

    public static InstitutionDeploymentAttempt Complete(InstitutionDeploymentAttempt attempt, RunResult run) =>
        attempt with
        {
            Status = run.Succeeded ? InstitutionDeploymentStatus.Succeeded
                : run.Outcomes.Any(x => x.Status == OutcomeStatus.Cancelled) ? InstitutionDeploymentStatus.Cancelled
                : InstitutionDeploymentStatus.Failed,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Outcomes = run.Outcomes,
            Issues = run.Outcomes.Where(x => !x.IsSuccessful).Select(x => $"{x.StepId}: {x.Status} ({x.Code})").ToImmutableArray(),
            Resources = attempt.Resources.ToImmutableDictionary(pair => pair.Key, pair => pair.Value with
            {
                Secrets = run.Outcomes.FirstOrDefault(x => x.StepId == "owned:" + pair.Key)?.Secrets ?? []
            })
        };
}
