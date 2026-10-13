using Aetheric.Provisioning.Engine;
using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Models.Post;

namespace Aetheric.Provisioning.Worker;

public sealed record InstitutionCatalogRequested(Guid RequestId, string? Environment = null, string? ParentDeploymentId = null);
public sealed record InstitutionCatalogReturned(Guid RequestId, IReadOnlyList<InstitutionDeployment> Institutions);

// Read-only query over the existing trusted provisioning broker. No secret values are returned.
public static class InstitutionCatalogPost
{
    public static IPostReference RequestReference() => new PostReference(ProvisioningPost.Domain, "institution/catalog",
        new PostContract("institution-catalog-requested", "1.0", PostIntent.Request));
    public static IPostReference ResultReference() => new PostReference(ProvisioningPost.Domain, "institution/catalog/result",
        new PostContract("institution-catalog-returned", "1.0", PostIntent.Response));
}

public sealed class InstitutionCatalogRequestConsumer(IPostProvider post, IInstitutionDeploymentStore store)
    : MessageConsumerBase<InstitutionCatalogRequested>
{
    public override IPostContract Contract => InstitutionCatalogPost.RequestReference().Contract;
    public override async Task ConsumeAsync(InstitutionCatalogRequested message, IPostContext context, CancellationToken ct = default)
    {
        var records = await store.ListAsync(message.Environment, message.ParentDeploymentId, ct);
        await post.PublishAsync(new PostEnvelope<InstitutionCatalogReturned>(InstitutionCatalogPost.ResultReference(),
            new(message.RequestId, records), new PostMetadata(correlationId: message.RequestId.ToString("D"),
                causationId: context.Envelope.Metadata.MessageId)), ct);
    }
}
