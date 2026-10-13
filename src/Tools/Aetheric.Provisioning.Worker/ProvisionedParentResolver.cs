using System.Collections.Immutable;
using System.Text.Json;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Workbench.Redis;

namespace Aetheric.Provisioning.Worker;

/// <summary>Resolves the nearest owning ancestors from provisioned records, not an admin-side resource list.</summary>
internal static class ProvisionedParentResolver
{
    public static async Task<ParentIdentity?> ResolveAsync(IInstitutionDeploymentStore? store, ParentIdentity? requested, CancellationToken ct)
    {
        if (requested?.DeploymentId is null) return requested; // Legacy callers still supply live-verifiable locations.
        if (store is null) throw new InvalidOperationException("A catalog parent requires a configured institution catalog.");
        var immediate = (await store.ReadAsync(requested.DeploymentId, ct))?.Provisioned
            ?? throw new InvalidOperationException("The selected parent has not been provisioned.");
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var locations = new Dictionary<string, string>(StringComparer.Ordinal);
        InstitutionDeploymentAttempt? current = immediate;
        while (current is not null)
        {
            if (!visited.Add(current.DeploymentId) || visited.Count > 64 || current.Environment != immediate.Environment)
                throw new InvalidDataException("Invalid provisioned institution containment chain.");
            foreach (var resource in current.Resources.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                var contract = resource.Provider switch
                {
                    "keycloak" when resource.Category == "identity" => "IRegistrar",
                    "mongodb" when resource.Category == "knowledge" => "ILibrary",
                    "s3" when resource.Category == "archive" => "IArchive",
                    "rabbitmq" when resource.Category == "post" => "IPostOffice",
                    "workbench" when resource.Category == "staging" => "IWorkbench",
                    _ => null
                };
                if (contract is null || locations.ContainsKey(contract)) continue;
                var location = contract switch
                {
                    "IRegistrar" => resource.Settings.GetValueOrDefault("realm"),
                    "ILibrary" when resource.Id == "library" => current.InstitutionId,
                    "IArchive" => resource.Settings.GetValueOrDefault("bucket"),
                    "IPostOffice" => resource.Settings.GetValueOrDefault("vhost"),
                    "IWorkbench" when resource.Settings.TryGetValue("stage", out var stage) => JsonSerializer.Serialize(
                        new WorkbenchParentLocation(1, current.Environment, current.InstitutionId, resource.Id, stage)),
                    _ => null
                };
                if (location is not null) locations[contract] = location;
            }
            if (current.Parent?.DeploymentId is not { } ancestorId) break;
            current = (await store.ReadAsync(ancestorId, ct))?.Provisioned
                ?? throw new InvalidDataException("A provisioned ancestor is missing from the catalog.");
        }
        return new ParentIdentity(immediate.DefinitionSource.Repository, immediate.DefinitionSource.Commit, locations.ToImmutableDictionary())
        { DeploymentId = immediate.DeploymentId };
    }
}
