using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Services;

namespace AethericForge.Runtime.Institutions.Security;

/// <summary>
/// Represents an Institution that manages the intake, investigation, and auditable resolution of
/// security incidents and compliance/audit findings.
/// </summary>
public interface ISecurity : IInstitution
{
    ISentinel Sentinel { get; }
}
