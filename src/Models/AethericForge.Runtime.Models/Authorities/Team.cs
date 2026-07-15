namespace AethericForge.Runtime.Models.Authorities;

using AethericForge.Runtime.Abstractions.Interfaces.Authorities;

using System.Collections;


/// <summary>
/// Represents an immutable team of members serving an authority.
/// </summary>
/// <typeparam name="TMember">The type of team member.</typeparam>
public sealed class Team<TMember> : ITeam<TMember>
{
    private readonly TMember[] _members;

    /// <summary>
    /// Initializes a team with the specified members.
    /// </summary>
    /// <param name="members">The members belonging to the team.</param>
    public Team(IEnumerable<TMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        _members = members.ToArray();

        if (_members.Any(member => member is null))
        {
            throw new ArgumentException(
                "A team cannot contain null members.",
                nameof(members));
        }
    }

    /// <inheritdoc />
    public int Count => _members.Length;

    /// <inheritdoc />
    public IEnumerator<TMember> GetEnumerator()
    {
        return ((IEnumerable<TMember>)_members).GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return _members.GetEnumerator();
    }
}