using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Institutions.PostOffice;

namespace AethericForge.Runtime.Services.Post;

public sealed class Postmaster(
    ITeam<IPostClerk> team,
    IPostExchange exchange) : IPostmaster
{
    private readonly ITeam<IPostClerk> _team = team ?? throw new ArgumentNullException(nameof(team));
    private readonly IPostExchange _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));

    public ITeam<IPostClerk> Team => _team;

    public Task<IPostReference> SendAsync(
        IPostEnvelope envelope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return _exchange.AcceptAsync(envelope, ct);
    }

    public Task<IPostEnvelope?> ReceiveAsync(
        IPostReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _exchange.CollectAsync(reference, ct);
    }
}
