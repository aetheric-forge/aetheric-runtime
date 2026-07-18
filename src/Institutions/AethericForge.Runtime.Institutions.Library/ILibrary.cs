using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;

namespace AethericForge.Runtime.Institutions.Library;

/// <summary>
/// Represents an Institution that serves as a repository of knowledge.
/// </summary>
public interface ILibrary : IInstitution
{
    
    ICurator Curator { get; }
    
    ILibrarian Librarian { get; }
}
