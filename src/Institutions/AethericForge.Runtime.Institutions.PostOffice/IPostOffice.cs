using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;

namespace AethericForge.Runtime.Institutions.PostOffice;

/// <summary>
/// Represents an Institution that exchanges post within an institutional
/// hierarchy.
/// </summary>
public interface IPostOffice : IInstitution
{
    IPostmaster Postmaster { get; }
    
    new IPostOfficeContext Context { get; }
}