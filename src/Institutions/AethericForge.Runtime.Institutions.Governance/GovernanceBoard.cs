using AethericForge.Runtime.Abstractions.Interfaces.Governance.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Governance;

public sealed class GovernanceBoard(IGovernanceContext context, ICouncil council)
    : InstitutionBase(context), IGovernanceBoard
{
    public new IGovernanceContext Context => (IGovernanceContext)base.Context;

    public ICouncil Council { get; } = council ?? throw new ArgumentNullException(nameof(council));
}
