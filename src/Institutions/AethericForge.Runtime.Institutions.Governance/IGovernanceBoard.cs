using AethericForge.Runtime.Abstractions.Interfaces.Governance.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;

namespace AethericForge.Runtime.Institutions.Governance;

/// <summary>
/// Represents an Institution led collectively by a Council rather than a single Dean - see
/// <see cref="ICouncil"/>. Sealed like <c>Faculty</c>, so a Campus that needs several boards
/// (Governance, and eventually others) gives each its own interface extending this one, since
/// <see cref="IInstitution.Register{TInstitution}"/> keys by exact type.
/// </summary>
public interface IGovernanceBoard : IInstitution
{
    ICouncil Council { get; }
}
