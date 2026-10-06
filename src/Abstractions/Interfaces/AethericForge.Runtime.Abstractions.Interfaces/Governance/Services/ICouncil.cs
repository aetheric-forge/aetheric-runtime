using AethericForge.Runtime.Abstractions.Interfaces.Authorities;

namespace AethericForge.Runtime.Abstractions.Interfaces.Governance.Services;

/// <summary>
/// Coordinates a Governance Board collectively, in contrast to <c>IDean</c>'s single title-bearing
/// leader - a Board has no one authority, only its seated members.
/// </summary>
public interface ICouncil : IAuthority<ICouncilMember>
{

}
