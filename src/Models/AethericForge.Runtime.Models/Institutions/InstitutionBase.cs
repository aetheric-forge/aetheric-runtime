using System.Diagnostics.CodeAnalysis;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Models.Institutions;

public abstract class InstitutionBase(IInstitutionContext context) : IInstitution
{
    private readonly Dictionary<Type, IInstitution> _institutions = new();
    private readonly Dictionary<string, IOrganization> _organizations = new();

    public IInstitutionContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

    public void Register<TInstitution>(TInstitution institution) where TInstitution : class, IInstitution
    {
        ArgumentNullException.ThrowIfNull(institution);

        if (!ReferenceEquals(institution.Context.Parent, this))
        {
            throw new ArgumentException(
                "The registered institution must belong to this institutional scope.",
                nameof(institution));
        }

        if (!_institutions.TryAdd(typeof(TInstitution), institution))
        {
            throw new InvalidOperationException(
                $"An institution is already registered for capability " +
                $"'{typeof(TInstitution).FullName}' in this institutional scope.");
        }
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

    public void RegisterOrganization(string id, IOrganization organization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(organization);

        if (!ReferenceEquals(organization.Context.Owner, this))
        {
            throw new ArgumentException(
                "The registered organization must be owned by this institutional scope.",
                nameof(organization));
        }

        if (!_organizations.TryAdd(id, organization))
        {
            throw new InvalidOperationException(
                $"An organization is already registered under id '{id}' in this institutional scope.");
        }
    }

    public bool TryResolveOrganization<TOrganization>(
        string id,
        [NotNullWhen(true)] out TOrganization? organization)
        where TOrganization : class, IOrganization
    {
        if (_organizations.TryGetValue(id, out var registered) && registered is TOrganization typed)
        {
            organization = typed;
            return true;
        }

        organization = null;
        return false;
    }

    public TOrganization ResolveOrganization<TOrganization>(string id)
        where TOrganization : class, IOrganization
    {
        if (TryResolveOrganization<TOrganization>(id, out var organization))
        {
            return organization;
        }

        throw new KeyNotFoundException(
            $"No organization registered under id '{id}' with contract " +
            $"'{typeof(TOrganization).FullName}' in this institutional scope.");
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

public class InstitutionContext(IInstitutionTemplate template, IServiceProvider services, IInstitution? parent = null)
    : IInstitutionContext
{
    public IInstitution? Parent { get; } = parent;
    public IInstitutionTemplate Template { get; } = template ?? throw new ArgumentNullException(nameof(template));
    public IServiceProvider Services { get; } = services ?? throw new ArgumentNullException(nameof(services));
}

/// <summary>
/// Base implementation for a concrete Organization. Deliberately carries only the context an Organization
/// needs - no registration/resolution of its own, since Organizations do not compose further Institutions
/// or Organizations and are not part of ancestor-scope capability resolution.
/// </summary>
public abstract class OrganizationBase(IOrganizationContext context) : IOrganization
{
    public IOrganizationContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));
}

public class OrganizationContext(IInstitutionTemplate template, IServiceProvider services, IInstitution owner)
    : IOrganizationContext
{
    public IInstitution Owner { get; } = owner ?? throw new ArgumentNullException(nameof(owner));
    public IInstitutionTemplate Template { get; } = template ?? throw new ArgumentNullException(nameof(template));
    public IServiceProvider Services { get; } = services ?? throw new ArgumentNullException(nameof(services));
}