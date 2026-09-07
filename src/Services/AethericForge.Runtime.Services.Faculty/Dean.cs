using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Faculty.Services;

namespace AethericForge.Runtime.Services.Faculty;

public sealed class Dean(string title, ITeam<IFacultyClerk> team) : IDean
{
    public string Title { get; } = !string.IsNullOrWhiteSpace(title)
        ? title
        : throw new ArgumentException("A Dean's title is required.", nameof(title));

    public ITeam<IFacultyClerk> Team { get; } = team ?? throw new ArgumentNullException(nameof(team));
}
