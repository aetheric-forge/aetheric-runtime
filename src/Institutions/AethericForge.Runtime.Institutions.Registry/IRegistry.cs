using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Institutions.Registry;

/// <summary>
/// Represents an Institution that manages identities and authentication.
/// </summary>
public interface IRegistry : IInstitution
{
    IRegistrar Registrar { get; }
}
