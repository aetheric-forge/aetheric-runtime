using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;

/// <summary>
/// Provides high-level archival services for objects, handling serialization and metadata automatically.
/// </summary>
public interface IArchivist
{
    /// <summary>
    /// Serializes and stores an object in the archive.
    /// </summary>
    /// <typeparam name="T">The type of the object to store.</typeparam>
    /// <param name="store">The name of the archive store.</param>
    /// <param name="key">The key identifying the object within the store.</param>
    /// <param name="value">The object to store.</param>
    /// <param name="contentType">The content type to use for serialization. If null, a default will be used based on available serializers.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A reference to the stored object.</returns>
    Task<IArchiveReference> PutAsync<T>(
        string store,
        string key,
        T value,
        string? contentType = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves and deserializes an object from the archive.
    /// </summary>
    /// <typeparam name="T">The type of the object to retrieve.</typeparam>
    /// <param name="reference">The reference to the archived object.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The deserialized object, or null if not found or incompatible.</returns>
    Task<T?> GetAsync<T>(
        IArchiveReference reference,
        CancellationToken ct = default);
}
