namespace AethericForge.Runtime.Institutions.Abstractions.Primitives;

public interface IInstitutionBootstrap
{
    Task BootstrapAsync(
        IServiceProvider services,
        CancellationToken cancellationToken);
}
