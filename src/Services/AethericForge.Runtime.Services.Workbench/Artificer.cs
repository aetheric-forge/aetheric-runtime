using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;

namespace AethericForge.Runtime.Services.Workbench;

public sealed class Artificer(ITeam<IWorkbenchWorker> team) : IArtificer
{
    public ITeam<IWorkbenchWorker> Team { get; } = team ?? throw new ArgumentNullException(nameof(team));
}
