using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;

namespace AethericForge.Runtime.Institutions.Library;

public interface ILibraryContext : IInstitutionContext
{
    IKnowledgeService Knowledge { get; }
}
