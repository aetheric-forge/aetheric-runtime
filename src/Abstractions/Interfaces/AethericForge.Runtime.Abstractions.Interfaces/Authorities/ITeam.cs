namespace AethericForge.Runtime.Abstractions.Interfaces.Authorities;

/// <summary>
/// Represents a read-only team of members serving an authority.
/// </summary>
/// <typeparam name="TMember">
/// The type of member belonging to the team.
/// </typeparam>
public interface ITeam<out TMember> : IReadOnlyCollection<TMember>
{
}