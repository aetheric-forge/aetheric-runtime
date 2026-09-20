using Aetheric.Provisioning.Application;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Keycloak;
using Aetheric.Provisioning.MongoDb;
using Aetheric.Provisioning.RabbitMq;
using Aetheric.Provisioning.S3;
using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Models.Post;

namespace Aetheric.Provisioning.Worker;

/// <summary>
/// A root institution (e.g. University) has no parent - none of its own resources declare a
/// "dependencies" contract, so its plan never generates a CheckParent step that would call this.
/// Also used, deliberately, as a safe fallback for a non-root institution whose request is
/// missing the Keycloak credential its real resolver would need: rather than crash, this reports
/// every contract as unavailable, which is honest ("cannot verify" -> "unavailable"), not silent
/// success.
/// </summary>
public sealed class NoParentCapabilityResolver : IParentCapabilityResolver
{
    public Task<bool> IsAvailableAsync(ParentContext parent, string contract, string source, CancellationToken ct) =>
        Task.FromResult(false);
}

/// <summary>The outcome of running one institution through DeployOneAsync.</summary>
public readonly record struct DeploymentOutcome(bool Succeeded, string[] Issues);

/// <summary>
/// Runs the plan/execute pipeline for a requested institution deployment and reports the outcome
/// back. Providers are built fresh per message from that message's own RootCredentials, not
/// shared across requests - ProviderContext carries no credentials of its own (matching
/// Workbench's pattern: the host builds each provider's connection, EnsureAsync itself stays
/// credential-agnostic), and credentials only exist for the lifetime of one request here. The
/// parent-capability resolver is built fresh per message too, for the same reason - it needs
/// live infrastructure access of its own when the deploying institution has a real parent
/// (message.Parent is non-null).
///
/// All four message-driven resources now have real providers ("rabbitmq", "mongodb", "keycloak",
/// "s3") - Stage 6's per-message credential flow is complete. Workbench is deliberately not
/// built here even though it also has a real provider (Aetheric.Provisioning.Workbench.Redis):
/// its backend takes a host-injected IDatabase (RedisWorkbenchBackend(IDatabase database)), not a
/// per-request credential, so it stays outside BuildProviders - a plan against the *whole* campus
/// (all five resources, including Workbench) still reports provider.unsupported for Workbench
/// specifically when driven through this Worker. That's a structural difference from the other
/// four, not a remaining Stage 6 gap.
///
/// Takes the review pipeline's stateless pieces via constructor injection - not built
/// per-message - so a test can substitute a fake IDefinitionSource instead of requiring live
/// GitHub access to exercise this consumer's logic.
///
/// DeployOneAsync/BuildProviders/BuildResolver are also reused by
/// InstitutionBootstrapRequestConsumer, which deploys four institutions inline (no
/// IDefinitionSource involved) through the identical configure/review/approve/execute sequence -
/// factored out here rather than duplicated so a fix to one (like the ParentContext.Capabilities
/// bug PR #29 found) can't silently apply to only one of the two consumers.
/// </summary>
public sealed class InstitutionDeploymentRequestConsumer(
    IPostProvider postProvider,
    IDefinitionSource source,
    InstitutionYamlReader reader,
    IRunStateStore runStateStore,
    ISecretStore secretStore)
    : MessageConsumerBase<InstitutionDeploymentRequested>
{
    public override IPostContract Contract => ProvisioningPost.RequestReference().Contract;

    public override async Task ConsumeAsync(InstitutionDeploymentRequested message, IPostContext context, CancellationToken ct = default)
    {
        var providers = BuildProviders(message.RootCredentials);
        var resolver = BuildResolver(message.Parent, message.RootCredentials);
        try
        {
            var planner = new ProvisioningPlanner(providers);
            var engine = new ProvisioningEngine(providers, resolver, runStateStore, secretStore);
            var review = new ProvisioningReview(source, reader, planner, engine);

            await review.LoadAsync(
                new DefinitionSourceRequest(message.Repository, message.Revision, message.DefinitionPath, message.BindingsPath),
                ct);

            var outcome = await DeployOneAsync(review, message.Parent, ct);

            var completed = new InstitutionDeploymentCompleted(message.RequestId, outcome.Succeeded, outcome.Issues, DateTimeOffset.UtcNow);
            var envelope = new PostEnvelope<InstitutionDeploymentCompleted>(
                ProvisioningPost.ResultReference(),
                completed,
                new PostMetadata(correlationId: message.RequestId.ToString()));

            await postProvider.PublishAsync(envelope, ct);
        }
        finally
        {
            foreach (var provider in providers) (provider as IDisposable)?.Dispose();
            (resolver as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// The shared configure -> review -> approve -> execute sequence every institution deployment
    /// runs, regardless of whether it was loaded via IDefinitionSource (this consumer) or
    /// LoadBundle (InstitutionBootstrapRequestConsumer) - the caller is responsible for loading
    /// review beforehand; this only runs once review.Loaded reflects whatever was loaded.
    /// </summary>
    internal static async Task<DeploymentOutcome> DeployOneAsync(ProvisioningReview review, ParentIdentity? parent, CancellationToken ct)
    {
        // ProvisioningPlanner.Plan requires a non-empty parent identity even for a root
        // institution with no actual parent-contract dependencies (ParentContext defaults to
        // ("", ""), which always fails context.missing) - LoadAsync/LoadBundle alone never call
        // Configure, so this is required for ANY plan to become valid, not just this one.
        // parent carries the deploying institution's *real* parent identity when it has one (e.g.
        // Campus's University); for a root institution (parent is null), the just-loaded
        // definition's own provenance doubles as a synthetic identity instead - "this
        // deployment's context is the commit it was loaded from," true regardless of what
        // institution it is, and never actually consulted since a root plan has no CheckParent
        // step to consult it.
        //
        // Capabilities is the deploying operator's own declared catalog - "I assert these
        // contracts resolve at these sources" - and ProvisioningReview.Review() requires it
        // to already match Bindings.ParentSources before a plan can even be produced (see
        // ReviewTests.cs's own established pattern: `Capabilities = bindings.ParentSources`).
        // This is not the live trust boundary - IParentCapabilityResolver.IsAvailableAsync,
        // called during ExecuteApprovedAsync, is what actually verifies the assertion against
        // live infrastructure. Skipping this leaves Review() rejecting every parent-dependent
        // plan with "parent.unresolved" before the resolver is ever reached at all.
        if (review.Loaded is not null)
        {
            var self = review.Loaded.Source.Definition.Provenance;
            var parentContext = parent is { } p
                ? new ParentContext(p.Repository, p.Revision)
                : new ParentContext(self.Repository, self.Commit);
            parentContext = parentContext with { Capabilities = review.Bindings!.ParentSources };
            review.Configure(review.Bindings!, parentContext);
        }

        var result = review.Review();
        if (!result.IsValid)
            return new DeploymentOutcome(false, review.Issues.Select(issue => $"{issue.Code}: {issue.Target} - {issue.Message}").ToArray());

        // Review() only plans - EnsureAsync is never called until the plan is approved and
        // executed. Auto-approve: this message *is* the operator's approval, there's no separate
        // review step for a machine-to-machine deployment request.
        review.Approve(result.Plan!.Id);
        var execution = await review.ExecuteApprovedAsync(ct: ct);
        var succeeded = execution.Run?.Succeeded ?? false;
        var issues = execution.Run is { } run
            ? run.Outcomes.Where(o => !o.IsSuccessful).Select(o => $"{o.StepId}: {o.Status} ({o.Code})").ToArray()
            : execution.Issues.Select(issue => $"{issue.Code}: {issue.Target} - {issue.Message}").ToArray();
        return new DeploymentOutcome(succeeded, issues);
    }

    internal static IReadOnlyList<IResourceProvider> BuildProviders(IReadOnlyDictionary<string, RootCredentialPayload> credentials)
    {
        var providers = new List<IResourceProvider>();
        if (credentials.TryGetValue("rabbitmq", out var rabbitMq))
        {
            providers.Add(new RabbitMqResourceProvider(new RootCredential(rabbitMq.Host, rabbitMq.Port, rabbitMq.Username, rabbitMq.Password)
            {
                RabbitMq = new RabbitMqRootOptions(rabbitMq.Scheme ?? "http", rabbitMq.BasePath ?? "/")
            }));
        }
        if (credentials.TryGetValue("mongo", out var mongo))
        {
            providers.Add(new MongoDbResourceProvider(new RootCredential(mongo.Host, mongo.Port, mongo.Username, mongo.Password)
            {
                Mongo = new MongoRootOptions(mongo.AuthDatabase ?? "admin")
            }));
        }
        if (credentials.TryGetValue("keycloak", out var keycloak))
        {
            providers.Add(new KeycloakResourceProvider(new RootCredential(keycloak.Host, keycloak.Port, keycloak.Username, keycloak.Password)
            {
                Keycloak = new KeycloakRootOptions(keycloak.Scheme ?? "https", keycloak.BasePath ?? "/", keycloak.Realm ?? "master")
            }));
        }
        if (credentials.TryGetValue("s3", out var s3))
        {
            providers.Add(new S3ResourceProvider(new RootCredential(s3.Host, s3.Port, s3.Username, s3.Password)
            {
                S3 = new S3RootOptions(s3.Scheme ?? "https")
            }));
        }
        return providers;
    }

    // parent is only non-null for a non-root institution (e.g. Campus). Builds one sub-resolver
    // per contract this deployment actually declared a location for - each needs both a root
    // credential for its system and a ParentIdentity.ResourceLocations entry; missing either
    // means that one contract just isn't recognized by the composite, which already means
    // "unavailable" (the correct fallback - "cannot verify" is not silent success). Falls back to
    // NoParentCapabilityResolver entirely only when parent itself is null (root institution) or
    // no sub-resolver could be built at all.
    internal static IParentCapabilityResolver BuildResolver(ParentIdentity? parent, IReadOnlyDictionary<string, RootCredentialPayload> credentials)
    {
        if (parent is not { } p) return new NoParentCapabilityResolver();

        var resolvers = new List<IParentCapabilityResolver>();
        if (p.ResourceLocations.TryGetValue("IRegistrar", out var realm) && credentials.TryGetValue("keycloak", out var keycloak))
        {
            resolvers.Add(new KeycloakRealmParentCapabilityResolver(new RootCredential(keycloak.Host, keycloak.Port, keycloak.Username, keycloak.Password)
            {
                Keycloak = new KeycloakRootOptions(keycloak.Scheme ?? "https", keycloak.BasePath ?? "/", keycloak.Realm ?? "master")
            }, realm));
        }
        if (p.ResourceLocations.TryGetValue("IPostOffice", out var vhost) && credentials.TryGetValue("rabbitmq", out var rabbitMq))
        {
            resolvers.Add(new RabbitMqVhostParentCapabilityResolver(new RootCredential(rabbitMq.Host, rabbitMq.Port, rabbitMq.Username, rabbitMq.Password)
            {
                RabbitMq = new RabbitMqRootOptions(rabbitMq.Scheme ?? "http", rabbitMq.BasePath ?? "/")
            }, vhost));
        }
        if (p.ResourceLocations.TryGetValue("IArchive", out var bucket) && credentials.TryGetValue("s3", out var s3))
        {
            resolvers.Add(new S3BucketParentCapabilityResolver(new RootCredential(s3.Host, s3.Port, s3.Username, s3.Password)
            {
                S3 = new S3RootOptions(s3.Scheme ?? "https")
            }, bucket));
        }
        if (p.ResourceLocations.TryGetValue("ILibrary", out var owningInstitutionId) && credentials.TryGetValue("mongo", out var mongo))
        {
            resolvers.Add(new MongoDbLibraryParentCapabilityResolver(new RootCredential(mongo.Host, mongo.Port, mongo.Username, mongo.Password)
            {
                Mongo = new MongoRootOptions(mongo.AuthDatabase ?? "admin")
            }, owningInstitutionId));
        }

        return resolvers.Count == 0 ? new NoParentCapabilityResolver() : new CompositeParentCapabilityResolver(resolvers);
    }
}
