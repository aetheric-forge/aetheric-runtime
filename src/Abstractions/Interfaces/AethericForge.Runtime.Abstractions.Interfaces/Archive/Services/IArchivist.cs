using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Authorities;

namespace AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;

/// <summary>
/// Provides high-level archival services for objects, handling serialization and metadata automatically.
/// </summary>
public interface IArchivist : IAuthority<IArchiveClerk>
{
    Task<IArchiveReference> PutAsync<T>(
        string store,
        string key,
        T value,
        string? contentType = null,
        CancellationToken ct = default);

    Task<T?> GetAsync<T>(
        IArchiveReference reference,
        CancellationToken ct = default);
}
