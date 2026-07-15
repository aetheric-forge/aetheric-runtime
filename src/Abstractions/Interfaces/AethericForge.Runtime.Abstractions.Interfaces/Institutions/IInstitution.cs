using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;

namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions;

public interface IInstitution
{
    IInstitutionContext Context { get; }
    
    IArchivist? Archivist { get; }
    
    IPostmaster? Postmaster { get; }
    
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
