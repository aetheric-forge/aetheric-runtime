namespace AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;

public interface IStagingObject
{
    IStagingReference Reference { get; }
    IStagingMetadata Metadata { get; }
}
