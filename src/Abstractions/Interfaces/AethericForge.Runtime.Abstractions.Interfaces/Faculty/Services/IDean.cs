using AethericForge.Runtime.Abstractions.Interfaces.Authorities;

namespace AethericForge.Runtime.Abstractions.Interfaces.Faculty.Services;

/// <summary>
/// Leads a Faculty. <see cref="Title"/> carries the division-specific honorific (e.g. "Principal
/// Architect", "Director", "Chief Engineer") that distinguishes one Faculty's Dean from another's
/// while sharing one concrete implementation.
/// </summary>
public interface IDean : IAuthority<IFacultyClerk>
{
    string Title { get; }
}
