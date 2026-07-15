using System.Diagnostics.CodeAnalysis;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Models.Institutions;

public abstract class InstitutionBase : IInstitution
{
    private readonly Dictionary<Type, IInstitution> _institutions = new();    
    
    protected InstitutionBase(IInstitutionContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IInstitutionContext Context { get; }
    public void Register<TInstitution>(TInstitution institution) where TInstitution : class, IInstitution
    {
        throw new NotImplementedException();
    }

    public bool TryResolve<TInstitution>(
        [NotNullWhen(true)] out TInstitution? institution)
        where TInstitution : class, IInstitution
    {
        if (_institutions.TryGetValue(
                typeof(TInstitution),
                out var registered))
        {
            institution = (TInstitution)registered;
            return true;
        }

        if (Context.Parent is not null)
        {
            return Context.Parent.TryResolve(out institution);
        }

        institution = null;
        return false;
    }
    
    public TInstitution Resolve<TInstitution>()
        where TInstitution : class, IInstitution
    {
        if (TryResolve<TInstitution>(out var institution))
        {
            return institution;
        }

        throw new KeyNotFoundException(
            $"No institution registered for capability " +
            $"'{typeof(TInstitution).FullName}' in this institutional scope " +
            $"or any ancestor scope.");
    }
    
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
    public InstitutionContext(IInstitutionTemplate template, IServiceProvider services, IInstitution? parent = null)
    {
        Template = template ?? throw new ArgumentNullException(nameof(template));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Parent = parent;
    }

    public IInstitution? Parent { get; }
    public IInstitutionTemplate Template { get; }
    public IServiceProvider Services { get; }
}