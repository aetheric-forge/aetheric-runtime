using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Institutions.Workbench;
using AethericForge.Runtime.Institutions.PostOffice;

namespace AethericForge.Runtime.Services.Post;

public sealed class PostExchange(IWorkbench workbench) : IPostExchange
{
    private readonly IWorkbench _workbench =
        workbench ?? throw new ArgumentNullException(nameof(workbench));

    public async Task<IPostReference> AcceptAsync(
        IPostEnvelope envelope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        await _workbench.WorkbenchService
            .PutAsync(envelope.Reference, envelope, ct)
            .ConfigureAwait(false);

        return envelope.Reference;
    }

    public Task<IPostEnvelope?> CollectAsync(
        IPostReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _workbench.WorkbenchService.GetAsync<IPostEnvelope>(reference, ct);
    }
}
