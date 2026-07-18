using AethericForge.Runtime.Models.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;

namespace AethericForge.Runtime.Institutions.Workbench;

public sealed class Workbench(
    IWorkbenchContext context,
    IArtificer artificer,
    IWorkbenchService workbenchService)
    : InstitutionBase(context), IWorkbench
{
    public new IWorkbenchContext Context => (IWorkbenchContext)base.Context;

    public IArtificer Artificer { get; } = artificer ?? throw new ArgumentNullException(nameof(artificer));
    public IWorkbenchService WorkbenchService { get; } = workbenchService ?? throw new ArgumentNullException(nameof(workbenchService));
}
