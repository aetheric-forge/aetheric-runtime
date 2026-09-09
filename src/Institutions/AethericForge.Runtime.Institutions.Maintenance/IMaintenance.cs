using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Services;

namespace AethericForge.Runtime.Institutions.Maintenance;

/// <summary>
/// Represents an Institution that manages the custody, execution, and history of maintenance jobs.
/// </summary>
public interface IMaintenance : IInstitution
{
    ICaretaker Caretaker { get; }
}
