using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Models.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Institutions.Workbench;

public class WorkbenchContext(
    IInstitutionTemplate template,
    IServiceProvider services,
    IInstitution? parent = null)
    : InstitutionContext(template, services, parent), IWorkbenchContext
{
}
