using Aetheric.Provisioning.Application;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.MongoDb;
using Aetheric.Provisioning.RabbitMq;
using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Models.Post;

namespace Aetheric.Provisioning.Worker;

/// <summary>
/// A template campus has no parent - institution/campus.yaml's five resources are all
/// ownership: "owned". This resolver exists only to satisfy ProvisioningEngine's constructor;
/// a root/no-parent plan never generates a CheckParent step that would call it.
/// </summary>
public sealed class NoParentCapabilityResolver : IParentCapabilityResolver
{
    public Task<bool> IsAvailableAsync(ParentContext parent, string contract, string source, CancellationToken ct) =>
        Task.FromResult(false);
}

/// <summary>
/// Runs the plan/execute pipeline for a requested campus deployment and reports the outcome
/// back. Providers are built fresh per message from that message's own RootCredentials, not
/// shared across requests - ProviderContext carries no credentials of its own (matching
/// Workbench's pattern: the host builds each provider's connection, EnsureAsync itself stays
/// credential-agnostic), and credentials only exist for the lifetime of one request here.
///
/// Only "rabbitmq" and "mongodb" have real providers today - Archive and Registry still
/// correctly report provider.unsupported. That's expected, not a bug: Stage 6 adds one
/// provider at a time, following the same pattern (Keycloak, S3 remain).
///
/// Takes the review pipeline's stateless pieces via constructor injection - not built
/// per-message - so a test can substitute a fake IDefinitionSource instead of requiring live
/// GitHub access to exercise this consumer's logic.
/// </summary>
public sealed class CampusDeploymentRequestConsumer(
    IPostProvider postProvider,
    IDefinitionSource source,
    InstitutionYamlReader reader,
    IParentCapabilityResolver resolver,
    IRunStateStore runStateStore,
    ISecretStore secretStore)
    : MessageConsumerBase<CampusDeploymentRequested>
{
    public override IPostContract Contract => ProvisioningPost.RequestReference().Contract;

    public override async Task ConsumeAsync(CampusDeploymentRequested message, IPostContext context, CancellationToken ct = default)
    {
        var providers = BuildProviders(message.RootCredentials);
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
            // The pinned definition source doubles as that identity: "this deployment's context
            // is the commit it was loaded from," which is true regardless of what institution it is.
            if (review.Loaded is not null)
                review.Configure(review.Bindings!, new ParentContext(message.Repository, message.Revision));

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

            var completed = new CampusDeploymentCompleted(message.RequestId, succeeded, issues, DateTimeOffset.UtcNow);
            var envelope = new PostEnvelope<CampusDeploymentCompleted>(
                ProvisioningPost.ResultReference(),
                completed,
                new PostMetadata(correlationId: message.RequestId.ToString()));

            await postProvider.PublishAsync(envelope, ct);
        }
        finally
        {
            foreach (var provider in providers) (provider as IDisposable)?.Dispose();
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
        return providers;
    }
}
