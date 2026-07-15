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

    Task InitializeAsync(
        CancellationToken cancellationToken = default);

    Task StartAsync(
        CancellationToken cancellationToken = default);

    Task StopAsync(
        CancellationToken cancellationToken = default);
}