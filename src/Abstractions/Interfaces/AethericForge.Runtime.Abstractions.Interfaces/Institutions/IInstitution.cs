using System.Diagnostics.CodeAnalysis;

namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions;

public interface IInstitution
{
    IInstitutionContext Context { get; }

    void Register<TInstitution>(TInstitution institution)
        where TInstitution : class, IInstitution;

    bool TryResolve<TInstitution>(
        [NotNullWhen(true)] out TInstitution? institution)
        where TInstitution : class, IInstitution;

    TInstitution Resolve<TInstitution>()
        where TInstitution : class, IInstitution;

    /// <summary>
    /// Registers an Organization under this Institution's scope, keyed by <paramref name="id"/> rather
    /// than by exclusive contract type - unlike <see cref="Register{TInstitution}"/>, several
    /// Organizations of the same shape can coexist here under different ids.
    /// </summary>
    void RegisterOrganization(string id, IOrganization organization);

    bool TryResolveOrganization<TOrganization>(
        string id,
        [NotNullWhen(true)] out TOrganization? organization)
        where TOrganization : class, IOrganization;

    TOrganization ResolveOrganization<TOrganization>(string id)
        where TOrganization : class, IOrganization;

    Task InitializeAsync(
        CancellationToken cancellationToken = default);

    Task StartAsync(
        CancellationToken cancellationToken = default);

    Task StopAsync(
        CancellationToken cancellationToken = default);
}