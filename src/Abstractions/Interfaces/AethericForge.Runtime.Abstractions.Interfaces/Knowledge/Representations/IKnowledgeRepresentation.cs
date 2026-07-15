namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;

public interface IKnowledgeRepresentation
{
    string ContentType { get; }
    string? Encoding { get; }
    string? Language { get; }
    long ContentLength { get; }
    string? ContentHash { get; }
    
    Task<Stream> OpenStreamAsync(CancellationToken cancellationToken = default);
}
