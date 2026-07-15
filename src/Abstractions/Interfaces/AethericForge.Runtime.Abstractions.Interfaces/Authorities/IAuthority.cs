namespace AethericForge.Runtime.Abstractions.Interfaces.Authorities;

/// <summary>
/// Represents an authority responsible for coordinating a team of members.
/// </summary>
/// <typeparam name="TMember">
/// The type of member coordinated by the authority.
/// </typeparam>
public interface IAuthority<out TMember>
{
    /// <summary>
    /// Gets the team coordinated by the authority.
    /// </summary>
    ITeam<TMember> Team { get; }
}