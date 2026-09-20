using System.Security.Cryptography;
using System.Text;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.Worker;

/// <summary>
/// Wraps operator-submitted definition+bindings YAML text (InstitutionConfig) into a SourceBundle
/// with synthetic provenance, for ProvisioningReview.LoadBundle - the inline-content counterpart
/// to InstitutionDeploymentRequestConsumer's IDefinitionSource-based, GitHub-pinned LoadAsync
/// path. ProvisioningPlanner.Plan and InstitutionYamlReader.Parse both require a real-shaped
/// SourceProvenance (HTTPS repository, 40-hex commit, actual SHA-256 content hash) even for
/// content that never came from git - Repository is an obviously-synthetic
/// "forms.aetheric-forge.internal" URI, Commit is a shared all-zero sentinel (harmless: plan
/// identity is a fingerprint of the whole PlanningInput, not the commit alone, so distinct
/// configs still produce distinct plan ids), ContentHash is computed here from the actual text.
/// </summary>
public static class InlineSourceDocuments
{
    private const string Commit = "0000000000000000000000000000000000000000";

    public static SourceBundle Build(string institutionId, InstitutionConfig config) => new(
        Document(institutionId, config.DefinitionYaml, "institution.yaml"),
        Document(institutionId, config.BindingsYaml, "institution.bindings.yaml"));

    private static SourceDocument Document(string institutionId, string text, string path)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var repository = $"https://forms.aetheric-forge.internal/{institutionId}";
        return new SourceDocument(text, new SourceProvenance(repository, Commit, path, hash));
    }
}
