using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;

namespace AethericForge.Runtime.Models.Knowledge.Representations;

public sealed class KnowledgeRepresentation : IKnowledgeRepresentation
{
    private readonly Func<CancellationToken, Task<Stream>> _streamFactory;

    public KnowledgeRepresentation(
        string contentType,
        long contentLength,
        Func<CancellationToken, Task<Stream>> streamFactory,
        string? encoding = null,
        string? language = null,
        string? contentHash = null)
    {
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        ContentLength = contentLength;
        _streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));
        Encoding = encoding;
        Language = language;
        ContentHash = contentHash;
    }

    public string ContentType { get; }
    public string? Encoding { get; }
    public string? Language { get; }
    public long ContentLength { get; }
    public string? ContentHash { get; }

    public Task<Stream> OpenStreamAsync(CancellationToken cancellationToken = default)
    {
        return _streamFactory(cancellationToken);
    }
}
