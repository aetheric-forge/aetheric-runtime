using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;

namespace AethericForge.Runtime.Models.Knowledge.Representations;

public sealed class KnowledgeRepresentation(
    string contentType,
    long contentLength,
    Func<CancellationToken, Task<Stream>> streamFactory,
    string? encoding = null,
    string? language = null,
    string? contentHash = null)
    : IKnowledgeRepresentation
{
    private readonly Func<CancellationToken, Task<Stream>> _streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));

    public string ContentType { get; } = contentType ?? throw new ArgumentNullException(nameof(contentType));
    public string? Encoding { get; } = encoding;
    public string? Language { get; } = language;
    public long ContentLength { get; } = contentLength;
    public string? ContentHash { get; } = contentHash;

    public Task<Stream> OpenStreamAsync(CancellationToken cancellationToken = default)
    {
        return _streamFactory(cancellationToken);
    }
}
