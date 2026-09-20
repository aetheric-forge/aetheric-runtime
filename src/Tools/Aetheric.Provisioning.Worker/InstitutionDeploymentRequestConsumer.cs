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
        var resolver = BuildResolver(message);
        try
        {
            var planner = new ProvisioningPlanner(providers);
            var engine = new ProvisioningEngine(providers, resolver, runStateStore, secretStore);
            var review = new ProvisioningReview(source, reader, planner, engine);

            await review.LoadAsync(
                new DefinitionSourceRequest(message.Repository, message.Revision, message.DefinitionPath, message.BindingsPath),
                ct);

            // ProvisioningPlanner.Plan requires a non-empty parent identity even for a root
            // institution with no actual parent-contract dependencies (ParentContext defaults to
            // ("", ""), which always fails context.missing) - LoadAsync alone never calls
            // Configure, so this is required for ANY plan to become valid, not just this one.
            // message.Parent carries the deploying institution's *real* parent identity when it
            // has one (e.g. Campus's University); for a root institution (message.Parent is
            // null), the pinned definition source doubles as a synthetic identity instead - "this
            // deployment's context is the commit it was loaded from," true regardless of what
            // institution it is, and never actually consulted since a root plan has no
            // CheckParent step to consult it.
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
                var parentContext = message.Parent is { } parent
                    ? new ParentContext(parent.Repository, parent.Revision)
                    : new ParentContext(message.Repository, message.Revision);
                parentContext = parentContext with { Capabilities = review.Bindings!.ParentSources };
                review.Configure(review.Bindings!, parentContext);
            }

            var result = review.Review();
            bool succeeded;
            string[] issues;
            if (!result.IsValid)
            {
                succeeded = false;
                issues = review.Issues.Select(issue => $"{issue.Code}: {issue.Target} - {issue.Message}").ToArray();
            }
            else
            {
                // Review() only plans - EnsureAsync is never called until the plan is approved
                // and executed. Auto-approve: this message *is* the operator's approval, there's
                // no separate review step for a machine-to-machine deployment request.
                review.Approve(result.Plan!.Id);
                var execution = await review.ExecuteApprovedAsync(ct: ct);
                succeeded = execution.Run?.Succeeded ?? false;
                issues = execution.Run is { } run
                    ? run.Outcomes.Where(o => !o.IsSuccessful).Select(o => $"{o.StepId}: {o.Status} ({o.Code})").ToArray()
                    : execution.Issues.Select(issue => $"{issue.Code}: {issue.Target} - {issue.Message}").ToArray();
            }

            var completed = new InstitutionDeploymentCompleted(message.RequestId, succeeded, issues, DateTimeOffset.UtcNow);
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

    private static IReadOnlyList<IResourceProvider> BuildProviders(IReadOnlyDictionary<string, RootCredentialPayload> credentials)
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

    // message.Parent is only non-null for a non-root institution (e.g. Campus) - and the only
    // real parent contract that exists today is Campus's IRegistrar dependency on University's
    // Keycloak-backed Registry, so this only ever needs to build one kind of resolver. A message
    // that claims a parent but is missing the "keycloak" credential falls back to
    // NoParentCapabilityResolver rather than crashing - "cannot verify" correctly reports as
    // "unavailable", not silent success.
    private static IParentCapabilityResolver BuildResolver(InstitutionDeploymentRequested message)
    {
        if (message.Parent is not { } parent) return new NoParentCapabilityResolver();
        if (!message.RootCredentials.TryGetValue("keycloak", out var keycloak)) return new NoParentCapabilityResolver();
        return new KeycloakRealmParentCapabilityResolver(new RootCredential(keycloak.Host, keycloak.Port, keycloak.Username, keycloak.Password)
        {
            Keycloak = new KeycloakRootOptions(keycloak.Scheme ?? "https", keycloak.BasePath ?? "/", keycloak.Realm ?? "master")
        }, parent.RegistryRealm);
    }
}
