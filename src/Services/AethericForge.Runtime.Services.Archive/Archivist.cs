using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Serialization;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Models.Archive.Primitives;
using AethericForge.Runtime.Models.Authorities;

namespace AethericForge.Runtime.Services.Archive;

public sealed class Archivist(ITeam<IArchiveClerk> team) : IArchivist
{
    private readonly IArchiveService _archiveService;
    private readonly ITeam<IArchiveClerk> _team;
    private readonly Dictionary<string, IArchiveSerializer> _serializers;
    private readonly string _defaultContentType;

    public Archivist(
        IArchiveService archiveService,
        ITeam<IArchiveClerk> team,
        IEnumerable<IArchiveSerializer> serializers) : this(team)
    {
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        _team = team;
        _serializers = (serializers ?? throw new ArgumentNullException(nameof(serializers)))
            .ToDictionary(s => s.ContentType, StringComparer.OrdinalIgnoreCase);

        if (_serializers.Count == 0)
        {
            throw new ArgumentException("At least one serializer must be provided.", nameof(serializers));
        }

        _defaultContentType = _serializers.ContainsKey("application/json")
            ? "application/json"
            : _serializers.Keys.First();
    }

    public async Task<IArchiveReference> PutAsync<T>(
        string store,
        string key,
        T value,
        string? contentType = null,
        CancellationToken ct = default)
    {
        contentType ??= _defaultContentType;

        if (!_serializers.TryGetValue(contentType, out var serializer))
        {
            throw new NotSupportedException($"No serializer found for content type '{contentType}'.");
        }

        using var stream = new MemoryStream();
        await serializer.SerializeAsync(stream, value, ct);
        stream.Position = 0;

        var metadata = new ArchiveMetadata(
            contentType: contentType, 
            contentLength: stream.Length);

        return await _archiveService.PutAsync(store, key, stream, metadata, ct);
    }

    public async Task<T?> GetAsync<T>(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        var metadata = await _archiveService.StatAsync(reference, ct);
        var contentType = metadata?.ContentType;

        if (string.IsNullOrEmpty(contentType))
        {
            return default;
        }

        if (!_serializers.TryGetValue(contentType, out var serializer))
        {
            return default;
        }

        using var stream = await _archiveService.RetrieveAsync(reference, ct);
        return await serializer.DeserializeAsync<T>(stream, ct);
    }

    public ITeam<IArchiveClerk> Team => _team;
}
