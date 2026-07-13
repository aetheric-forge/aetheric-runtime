using AethericForge.Runtime.Abstractions.Interfaces.Storage.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Storage.Providers;

using AethericForge.Runtime.Abstractions.Interfaces.Storage;

public interface IStorageProvider
{
    string Store { get; }

    Task<IStorageReference> PutAsync(
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
