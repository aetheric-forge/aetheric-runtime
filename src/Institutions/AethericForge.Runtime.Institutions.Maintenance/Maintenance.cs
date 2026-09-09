using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Maintenance;

public sealed class Maintenance(IMaintenanceContext context, ICaretaker caretaker)
    : InstitutionBase(context), IMaintenance
{
    public new IMaintenanceContext Context => (IMaintenanceContext)base.Context;

    public ICaretaker Caretaker { get; } = caretaker ?? throw new ArgumentNullException(nameof(caretaker));
}
