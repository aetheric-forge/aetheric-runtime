using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Governance.Services;

namespace AethericForge.Runtime.Services.Governance;

public sealed class Council(ITeam<ICouncilMember> team) : ICouncil
{
    public ITeam<ICouncilMember> Team { get; } = team ?? throw new ArgumentNullException(nameof(team));
}
