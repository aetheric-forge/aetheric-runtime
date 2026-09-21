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
/// Runs in two passes. Preflight (pure, no live I/O) parses and statically plans all four before
/// executing any of them - provisioning isn't transactional, so University could otherwise be
/// fully (and irreversibly) provisioned before a syntax error or an unsupported provider in
/// Campus's own YAML is ever discovered. Only if every one of the four passes preflight does
/// execution begin; any *execution-time* failure still short-circuits the rest (reported
/// NotAttempted) exactly as before - that risk class (a resource an ancestor claims to own but
/// doesn't actually have live) can't be preflighted without side effects, and isn't what the
/// preflight pass addresses.
///
/// Nearest owning ancestor: each level's own submitted bindings decide, per resource, whether it
/// privatizes (ownership: owned) or inherits (ownership: parent, a dependencies entry) each of
/// Registry/Archive/Library/Post Office independently - a level can privatize one and inherit the
/// rest. Since privatization means the nearest ancestor in the org chart isn't necessarily the
/// nearest ancestor that *owns* a given resource, this consumer resolves each dependency itself
/// by walking the chain of already-parsed ancestors from nearest to furthest (University is the
/// base case for Registry - it always owns it, per docs/specs/university.md §6) and using
/// whichever level's own submitted setting actually owns it. This is resolved statically from the
/// four submitted definitions, not via a live multi-hop lookup service - the Worker already has
/// everything it needs before deploying anything.
/// </summary>
public sealed class InstitutionBootstrapRequestConsumer(
    IPostProvider postProvider,
    InstitutionYamlReader reader,
    IRunStateStore runStateStore,
    ISecretStore secretStore,
    string workbenchTarget)
    : MessageConsumerBase<InstitutionBootstrapRequested>
{
    private static readonly (string Step, string Id)[] Slots =
    [
        ("University", "university"),
        ("Campus", "campus"),
        ("AdministrationFaculty", "administration-faculty"),
        ("Decisions", "decisions"),
    ];

    public override IPostContract Contract => ProvisioningBootstrapPost.RequestReference().Contract;

    public override async Task ConsumeAsync(InstitutionBootstrapRequested message, IPostContext context, CancellationToken ct = default)
    {
        var configs = new[] { message.University, message.Campus, message.AdministrationFaculty, message.Decisions };
        var parsed = new (SourceBundle Bundle, LoadedInstitution? Institution, ImmutableArray<ValidationIssue> Issues)[Slots.Length];
        for (var i = 0; i < Slots.Length; i++)
        {
            var bundle = InlineSourceDocuments.Build(Slots[i].Id, configs[i]);
            var result = reader.Read(bundle);
            parsed[i] = (bundle, result.Institution, result.Issues);
        }

        var (chain, preflight, preflightPassed) = Preflight(parsed, message.RootCredentials, workbenchTarget);
        if (!preflightPassed)
        {
            await PublishCompletionAsync(message, context, false, preflight, ct);
            return;
        }

        var executed = await ExecuteAsync(parsed, chain, message.RootCredentials, ct);
        await PublishCompletionAsync(message, context, executed.Succeeded, executed.Steps, ct);
    }

    /// <summary>
    /// Parses are already done by the time this runs. Statically plans each of the four (a raw
    /// ProvisioningPlanner.Plan() call - no ProvisioningReview/Engine involved, nothing executed)
    /// to catch provider.unsupported/dependency.binding_missing/structural binding issues before
    /// any live infrastructure is touched. A level that failed to parse halts chain growth (a
    /// later level's dependency resolution against a nonexistent ancestor isn't meaningful) but
    /// every level still gets its own real, independent preflight verdict - a downstream level
    /// reporting Succeeded here means "my own definition is structurally sound, assuming my
    /// declared ancestor exists," not a claim that an earlier-failed ancestor is actually fine;
    /// the envelope as a whole still won't execute if anything failed preflight.
    /// </summary>
    private static (List<DeployedLevel> Chain, ImmutableArray<BootstrapStepResult> Results, bool Passed) Preflight(
        (SourceBundle Bundle, LoadedInstitution? Institution, ImmutableArray<ValidationIssue> Issues)[] parsed,
        IReadOnlyDictionary<string, RootCredentialPayload> credentials,
        string workbenchTarget)
    {
        var chain = new List<DeployedLevel>();
        var results = ImmutableArray.CreateBuilder<BootstrapStepResult>(Slots.Length);
        var passed = true;

        for (var i = 0; i < parsed.Length; i++)
        {
            var (_, institution, parseIssues) = parsed[i];
            if (institution is null)
            {
                passed = false;
                results.Add(new BootstrapStepResult(Slots[i].Step, BootstrapStepStatus.Failed,
                    parseIssues.Select(issue => $"{issue.Code}: {issue.Target} - {issue.Message}").ToArray()));
                continue;
            }

            var parent = BuildParentIdentity(chain, institution.Requirements);
            var providers = InstitutionDeploymentRequestConsumer.BuildProviders(credentials, workbenchTarget);
            try
            {
                var parentContext = InstitutionDeploymentRequestConsumer.ResolveParentContext(institution, parent);
                var input = new PlanningInput(institution.Requirements, institution.Bindings, parentContext,
                    institution.Source.Definition.Provenance, institution.Source.Bindings.Provenance);
                var planResult = new ProvisioningPlanner(providers).Plan(input);
                if (!planResult.IsValid) passed = false;
                results.Add(new BootstrapStepResult(Slots[i].Step,
                    planResult.IsValid ? BootstrapStepStatus.Succeeded : BootstrapStepStatus.Failed,
                    planResult.Issues.Select(issue => $"{issue.Code}: {issue.Target} - {issue.Message}").ToArray()));
            }
            finally
            {
                foreach (var provider in providers) (provider as IDisposable)?.Dispose();
            }

            chain.Add(new DeployedLevel(institution.Source.Definition.Provenance, institution.Requirements, institution.Bindings));
        }

        return (chain, results.MoveToImmutable(), passed);
    }

    /// <summary>Every level already passed preflight, so this rebuilds each parent identity against the same (now-complete) chain rather than reuse Preflight's - only the prefix up to the current level, matching Preflight's own incremental view at that point.</summary>
    private async Task<(bool Succeeded, ImmutableArray<BootstrapStepResult> Steps)> ExecuteAsync(
        (SourceBundle Bundle, LoadedInstitution? Institution, ImmutableArray<ValidationIssue> Issues)[] parsed,
        List<DeployedLevel> chain,
        IReadOnlyDictionary<string, RootCredentialPayload> credentials,
        CancellationToken ct)
    {
        var steps = ImmutableArray.CreateBuilder<BootstrapStepResult>(Slots.Length);
        var allSucceeded = true;

        for (var i = 0; i < parsed.Length; i++)
        {
            if (!allSucceeded)
            {
                steps.Add(new BootstrapStepResult(Slots[i].Step, BootstrapStepStatus.NotAttempted, []));
                continue;
            }

            var (bundle, institution, _) = parsed[i];
            var parent = BuildParentIdentity(chain.GetRange(0, i), institution!.Requirements);
            var providers = InstitutionDeploymentRequestConsumer.BuildProviders(credentials, workbenchTarget);
            var resolver = InstitutionDeploymentRequestConsumer.BuildResolver(parent, credentials);
            try
            {
                var planner = new ProvisioningPlanner(providers);
                var engine = new ProvisioningEngine(providers, resolver, runStateStore, secretStore);
                var review = new ProvisioningReview(UnusedDefinitionSource.Instance, reader, planner, engine);
                review.LoadBundle(bundle);

                var outcome = await InstitutionDeploymentRequestConsumer.DeployOneAsync(review, parent, ct);
                steps.Add(new BootstrapStepResult(Slots[i].Step,
                    outcome.Succeeded ? BootstrapStepStatus.Succeeded : BootstrapStepStatus.Failed, outcome.Issues));
                if (!outcome.Succeeded) allSucceeded = false;
            }
            finally
            {
                foreach (var provider in providers) (provider as IDisposable)?.Dispose();
                (resolver as IDisposable)?.Dispose();
            }
        }

        return (allSucceeded, steps.MoveToImmutable());
    }

    private async Task PublishCompletionAsync(InstitutionBootstrapRequested message, IPostContext context,
        bool succeeded, ImmutableArray<BootstrapStepResult> steps, CancellationToken ct)
    {
        var completed = new InstitutionBootstrapCompleted(message.RequestId, succeeded, steps, DateTimeOffset.UtcNow);
        var initiator = context.Envelope.Metadata;
        var envelope = new PostEnvelope<InstitutionBootstrapCompleted>(
            ProvisioningBootstrapPost.ResultReference(),
            completed,
            BootstrapPostMetadata.CreateCompletion(initiator, completedAtUtc: completed.CompletedAtUtc));
        await postProvider.PublishAsync(envelope, ct);
    }

    internal sealed record DeployedLevel(SourceProvenance Provenance, InstitutionRequirements Requirements, DeploymentBindings Bindings);

    // Maps each contract this Worker can verify to the resource id institution.yaml files use for
    // it (institution/campus.yaml's own resource ids) and the bindings setting key that names its
    // location - Library is the one exception, since its live check verifies a scoped user (see
    // ResolveLocation below), not a location setting at all.
    private static readonly ImmutableDictionary<string, (string ResourceId, string? SettingKey)> ContractResources =
        ImmutableDictionary.CreateRange(
        [
            KeyValuePair.Create("IRegistrar", ("registry", (string?)"realm")),
            KeyValuePair.Create("IArchive", ("archive", (string?)"bucket")),
            KeyValuePair.Create("IPostOffice", ("post-office", (string?)"vhost")),
            KeyValuePair.Create("ILibrary", ("library", (string?)null)),
        ]);

    internal static ParentIdentity? BuildParentIdentity(IReadOnlyList<DeployedLevel> chain, InstitutionRequirements requirements)
    {
        if (requirements.ParentContracts.IsDefaultOrEmpty || chain.Count == 0) return null;
        var locations = ImmutableDictionary.CreateBuilder<string, string>();
        foreach (var contract in requirements.ParentContracts)
        {
            var location = ResolveLocation(chain, contract);
            if (location is not null) locations[contract] = location;
        }
        // BuildResolver falls back to NoParentCapabilityResolver for any contract with no
        // resolved location, correctly reporting it unavailable rather than crashing - so an
        // empty map here still produces a real (if uniformly-unavailable) parent identity.
        var immediateParent = chain[^1];
        return new ParentIdentity(immediateParent.Provenance.Repository, immediateParent.Provenance.Commit, locations.ToImmutable());
    }

    // Walks the chain from nearest ancestor to furthest, returning the first level whose own
    // definition owns this contract's resource. Every contract except ILibrary resolves to that
    // level's own binding setting (realm/bucket/vhost); ILibrary resolves to the owning level's
    // own institution id instead, since MongoDbLibraryParentCapabilityResolver verifies the
    // scoped user ("{owningInstitutionId}-library") that level's own MongoDbResourceProvider
    // actually created, not the database itself (Mongo creates databases lazily on write, and
    // that provider never writes data, so "does the database exist" isn't a reliable signal).
    internal static string? ResolveLocation(IReadOnlyList<DeployedLevel> chain, string contract)
    {
        if (!ContractResources.TryGetValue(contract, out var resource)) return null;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var level = chain[i];
            if (!level.Requirements.Resources.Any(r => r.Id == resource.ResourceId && r.Ownership == "owned")) continue;
            if (resource.SettingKey is null) return level.Requirements.Id;
            if (level.Bindings.Resources.TryGetValue(resource.ResourceId, out var binding) && binding.Settings.TryGetValue(resource.SettingKey, out var value))
                return value;
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
