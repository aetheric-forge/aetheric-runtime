using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions;

public interface IInstitutionContext
{
    IInstitution? Parent { get; }
    IInstitutionTemplate Template { get; }
    IServiceProvider Services { get; }
}