using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Archive;

/// <summary>
/// Sealed implementation of <see cref="IArchive"/>.
/// </summary>
public sealed class Archive(
    IArchiveContext context,
    IArchiveVault vault,
    IArchivist archivist)
    : InstitutionBase(context), IArchive
{
    private readonly IArchiveVault _vault =
        vault ?? throw new ArgumentNullException(nameof(vault));

    public override IArchivist Archivist { get; } =
        archivist ?? throw new ArgumentNullException(nameof(archivist));

    public new IArchiveContext Context { get; } =
        context ?? throw new ArgumentNullException(nameof(context));

    public Task<IArchiveReference> ArchiveAsync(
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return _vault.ArchiveAsync(content, metadata, ct);
    }

    public Task<Stream> RetrieveAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _vault.RetrieveAsync(reference, ct);
    }

    public Task<IArchiveMetadata?> StatAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _vault.StatAsync(reference, ct);
    }

    public Task<bool> ExistsAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _vault.ExistsAsync(reference, ct);
    }

    public Task<bool> DeleteAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _vault.DeleteAsync(reference, ct);
    }
}
