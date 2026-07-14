using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Models.Institutions;

public abstract class InstitutionBase : IInstitution
{
    protected InstitutionBase(IInstitutionContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IInstitutionContext Context { get; }

    public virtual Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class InstitutionContext : IInstitutionContext
{
    public InstitutionContext(IInstitutionTemplate template, IServiceProvider services)
    {
        Template = template ?? throw new ArgumentNullException(nameof(template));
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IInstitutionTemplate Template { get; }
    public IServiceProvider Services { get; }
}