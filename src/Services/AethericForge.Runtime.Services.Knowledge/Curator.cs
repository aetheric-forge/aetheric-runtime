using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;

namespace AethericForge.Runtime.Services.Knowledge;

public sealed class Curator : ICurator
{
    public Curator(ITeam<ICuratorClerk> team)
    {
        Team = team ?? throw new ArgumentNullException(nameof(team));
    }

    public ITeam<ICuratorClerk> Team { get; }
}
