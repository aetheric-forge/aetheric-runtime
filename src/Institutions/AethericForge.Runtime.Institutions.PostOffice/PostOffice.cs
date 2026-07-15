using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.PostOffice;

public sealed class PostOffice(
    IPostOfficeContext context,
    IPostExchange exchange,
    IPostmaster postmaster)
    : InstitutionBase(context), IPostOffice
{
    private readonly IPostExchange _exchange =
        exchange ?? throw new ArgumentNullException(nameof(exchange));

    public IPostmaster Postmaster { get; } =
        postmaster ?? throw new ArgumentNullException(nameof(postmaster));
    
    public new IPostOfficeContext Context { get; } =
        context ?? throw new ArgumentNullException(nameof(context));

    public Task<IPostReference> AcceptAsync(
        IPostEnvelope envelope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return _exchange.AcceptAsync(envelope, ct);
    }

    public Task<IPostEnvelope?> CollectAsync(
        IPostReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _exchange.CollectAsync(reference, ct);
    }
}