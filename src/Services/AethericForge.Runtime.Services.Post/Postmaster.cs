using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Institutions.PostOffice;

namespace AethericForge.Runtime.Services.Post;

public sealed class Postmaster(
    ITeam<IPostClerk> team,
    IPostService service) : IPostmaster
{
    private readonly ITeam<IPostClerk> _team = team ?? throw new ArgumentNullException(nameof(team));
    private readonly IPostService _service = service ?? throw new ArgumentNullException(nameof(service));

    public ITeam<IPostClerk> Team => _team;

    public Task<IPostReference> AcceptAsync(
        IPostEnvelope envelope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return _service.AcceptAsync(envelope, ct);
    }

    public Task<IPostEnvelope?> CollectAsync(
        IPostReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _service.CollectAsync(reference, ct);
    }
}
