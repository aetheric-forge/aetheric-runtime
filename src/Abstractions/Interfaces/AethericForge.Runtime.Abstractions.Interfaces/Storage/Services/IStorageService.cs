using AethericForge.Runtime.Abstractions.Interfaces.Storage.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Storage.Services;

using AethericForge.Runtime.Abstractions.Interfaces.Storage;

public interface IStorageService
{
    Task<IStorageReference> PutAsync(
        string store,
        string key,
        Stream content,
        IStorageMetadata? metadata = null,
        CancellationToken ct = default);

    Task<Stream> OpenReadAsync(
        IStorageReference reference,
        CancellationToken ct = default);

    Task<IStorageMetadata?> StatAsync(
        IStorageReference reference,
        CancellationToken ct = default);

    Task<bool> ExistsAsync(
        IStorageReference reference,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        IStorageReference reference,
        CancellationToken ct = default);
}
