using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;

namespace AethericForge.Runtime.Institutions.Archive;

/// <summary>
/// Represents an Institution that provides archival storage and retrieval.
/// </summary>
public interface IArchive : IInstitution
{
    /// <summary>
    /// Archives the provided content.
    /// </summary>
    /// <param name="content">The content stream to archive.</param>
    /// <param name="metadata">Optional metadata to associate with the content.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A reference to the archived content.</returns>
    Task<IArchiveReference> ArchiveAsync(
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the content identified by the reference.
    /// </summary>
    /// <param name="reference">The reference identifying the content.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A stream of the content.</returns>
    Task<Stream> RetrieveAsync(
        IArchiveReference reference,
        CancellationToken ct = default);

    /// <summary>
    /// Gets metadata for the content identified by the reference.
    /// </summary>
    /// <param name="reference">The reference identifying the content.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The metadata, or null if not found.</returns>
    Task<IArchiveMetadata?> StatAsync(
        IArchiveReference reference,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if content exists for the given reference.
    /// </summary>
    /// <param name="reference">The reference identifying the content.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if it exists, false otherwise.</returns>
    Task<bool> ExistsAsync(
        IArchiveReference reference,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the content identified by the reference.
    /// </summary>
    /// <param name="reference">The reference identifying the content.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deleted, false if it didn't exist.</returns>
    Task<bool> DeleteAsync(
        IArchiveReference reference,
        CancellationToken ct = default);
}