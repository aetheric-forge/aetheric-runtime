using System.Collections.Immutable;
using Aetheric.Provisioning.Application;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Models.Post;

namespace Aetheric.Provisioning.Worker;

/// <summary>
/// Bootstraps University -> Campus -> Administration Faculty -> Decisions Institution in one
/// operator action. Every level is deployed through the identical
/// InstitutionDeploymentRequestConsumer.DeployOneAsync sequence University/Campus already use -
/// nothing here is special-cased by level; "Administration Faculty" is provisioned exactly like
/// Campus or Decisions, not treated as composition-only (its runtime IFaculty/DI-composition
/// question is a separate, already-deferred concern from what gets provisioned here).
///
/// Any provisioning failure short-circuits the rest of the sequence - the remaining steps are
/// reported NotAttempted, not silently skipped or run anyway.
///
/// Nearest owning ancestor: each level's own submitted bindings decide whether it privatizes
/// Registry (ownership: owned) or inherits it (ownership: parent, dependencies: [IRegistrar]).
/// Since privatization means the nearest ancestor in the org chart isn't necessarily the nearest
/// ancestor that *owns* the resource, this consumer resolves it itself by walking the chain of
/// already-deployed ancestors from nearest to furthest (University is the base case - it always
/// owns Registry, per docs/specs/university.md §6) and using whichever level's own submitted
/// realm setting actually owns it. This is resolved statically from the four submitted
/// definitions, not via a live multi-hop lookup service - the Worker already has everything it
/// needs before deploying anything.
/// </summary>
public sealed class InstitutionBootstrapRequestConsumer(
    IPostProvider postProvider,
    InstitutionYamlReader reader,
    IRunStateStore runStateStore,
    ISecretStore secretStore)
    : MessageConsumerBase<InstitutionBootstrapRequested>
{
    public override IPostContract Contract => ProvisioningBootstrapPost.RequestReference().Contract;

    public override async Task ConsumeAsync(InstitutionBootstrapRequested message, IPostContext context, CancellationToken ct = default)
    {
        var steps = ImmutableArray.CreateBuilder<BootstrapStepResult>();
        var chain = new List<DeployedLevel>();
        var allSucceeded = true;

        var items = new (string Step, string Id, InstitutionConfig Config)[]
        {
            ("University", "university", message.University),
            ("Campus", "campus", message.Campus),
            ("AdministrationFaculty", "administration-faculty", message.AdministrationFaculty),
            ("Decisions", "decisions", message.Decisions),
        };

        foreach (var (step, id, config) in items)
        {
            if (!allSucceeded)
            {
                steps.Add(new BootstrapStepResult(step, BootstrapStepStatus.NotAttempted, []));
                continue;
            }

            var bundle = InlineSourceDocuments.Build(id, config);
            // Requirements/Bindings are needed to choose a parent identity before the real
            // ProvisioningEngine/resolver can be constructed (their constructors are immutable) -
            // reader.Read is a pure parse with no I/O, so parsing once here and again inside
            // review.LoadBundle below is cheap and side-effect-free, not a real duplication cost.
            var preParse = reader.Read(bundle);
            if (preParse.Institution is null)
            {
                allSucceeded = false;
                steps.Add(new BootstrapStepResult(step, BootstrapStepStatus.Failed,
                    preParse.Issues.Select(issue => $"{issue.Code}: {issue.Target} - {issue.Message}").ToArray()));
                continue;
            }

            var parent = BuildParentIdentity(chain, preParse.Institution.Requirements);
            var providers = InstitutionDeploymentRequestConsumer.BuildProviders(message.RootCredentials);
            var resolver = InstitutionDeploymentRequestConsumer.BuildResolver(parent, message.RootCredentials);
            try
            {
                var planner = new ProvisioningPlanner(providers);
                var engine = new ProvisioningEngine(providers, resolver, runStateStore, secretStore);
                var review = new ProvisioningReview(UnusedDefinitionSource.Instance, reader, planner, engine);
                review.LoadBundle(bundle);

                var outcome = await InstitutionDeploymentRequestConsumer.DeployOneAsync(review, parent, ct);
                steps.Add(new BootstrapStepResult(step,
                    outcome.Succeeded ? BootstrapStepStatus.Succeeded : BootstrapStepStatus.Failed, outcome.Issues));

                if (outcome.Succeeded)
                    chain.Add(new DeployedLevel(preParse.Institution.Source.Definition.Provenance,
                        preParse.Institution.Requirements, preParse.Institution.Bindings));
                else
                    allSucceeded = false;
            }
            finally
            {
                foreach (var provider in providers) (provider as IDisposable)?.Dispose();
                (resolver as IDisposable)?.Dispose();
            }
        }

        var completed = new InstitutionBootstrapCompleted(message.RequestId, allSucceeded, steps.ToImmutable(), DateTimeOffset.UtcNow);
        var initiator = context.Envelope.Metadata;
        var envelope = new PostEnvelope<InstitutionBootstrapCompleted>(
            ProvisioningBootstrapPost.ResultReference(),
            completed,
            new PostMetadata(
                correlationId: initiator.CorrelationId ?? initiator.MessageId,
                causationId: initiator.MessageId));

        await postProvider.PublishAsync(envelope, ct);
    }

    internal sealed record DeployedLevel(SourceProvenance Provenance, InstitutionRequirements Requirements, DeploymentBindings Bindings);

    internal static ParentIdentity? BuildParentIdentity(IReadOnlyList<DeployedLevel> chain, InstitutionRequirements requirements)
    {
        if (requirements.ParentContracts.IsDefaultOrEmpty) return null;
        var realm = ResolveRegistryRealm(chain);
        if (realm is null || chain.Count == 0) return null; // BuildResolver falls back to NoParentCapabilityResolver, correctly reporting unavailable
        var immediateParent = chain[^1];
        return new ParentIdentity(immediateParent.Provenance.Repository, immediateParent.Provenance.Commit, realm);
    }

    internal static string? ResolveRegistryRealm(IReadOnlyList<DeployedLevel> chain)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var level = chain[i];
            if (level.Requirements.Resources.Any(r => r.Id == "registry" && r.Ownership == "owned")
                && level.Bindings.Resources.TryGetValue("registry", out var binding)
                && binding.Settings.TryGetValue("realm", out var realm))
                return realm;
        }
        return null;
    }

    /// <summary>Never actually invoked - LoadBundle bypasses IDefinitionSource entirely; ProvisioningReview's constructor still requires one.</summary>
    private sealed class UnusedDefinitionSource : IDefinitionSource
    {
        public static readonly UnusedDefinitionSource Instance = new();
        public Task<SourceLoadResult> LoadAsync(DefinitionSourceRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException("InstitutionBootstrapRequestConsumer only uses LoadBundle - this should never be called.");
    }
}
