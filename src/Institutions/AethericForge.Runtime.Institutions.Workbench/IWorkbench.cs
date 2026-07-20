using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;

namespace AethericForge.Runtime.Institutions.Workbench;

public interface IWorkbench : IInstitution
{
    IArtificer Artificer { get; }
    IWorkbenchService WorkbenchService { get; }
}
