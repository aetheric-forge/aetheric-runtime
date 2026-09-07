using AethericForge.Runtime.Abstractions.Interfaces.Faculty.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;

namespace AethericForge.Runtime.Institutions.Faculty;

/// <summary>
/// Represents an Institution that groups other Institutions under a single Dean. A Campus (or,
/// eventually, another Faculty) registers one Faculty per division, each behind its own interface
/// extending this one, since <see cref="IInstitution.Register{TInstitution}"/> keys by exact type.
/// </summary>
public interface IFaculty : IInstitution
{
    IDean Dean { get; }
}
