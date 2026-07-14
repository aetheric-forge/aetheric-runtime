namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions;

public interface IInstitution
{
    IInstitutionContext Context { get; }
    
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
