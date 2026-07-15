using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Campus;

public sealed class Campus(ICampusContext context) : InstitutionBase(context), ICampus
{
    public new ICampusContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));
    
    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return base.InitializeAsync(cancellationToken);
    }

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        return base.StopAsync(cancellationToken);
    }
}