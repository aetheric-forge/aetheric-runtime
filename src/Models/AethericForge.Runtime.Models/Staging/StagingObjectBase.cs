using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;

namespace AethericForge.Runtime.Models.Staging;

public abstract class StagingObjectBase : IStagingObject
{
    protected StagingObjectBase(
        IStagingReference reference,
        IStagingMetadata? metadata = null)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        Metadata = metadata ?? new StagingMetadata();
    }

    public IStagingReference Reference { get; }
    public IStagingMetadata Metadata { get; }
}
