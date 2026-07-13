using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;

namespace AethericForge.Runtime.Models.Staging;

public sealed class StagingObject : StagingObjectBase
{
    public StagingObject(
        IStagingReference reference,
        IStagingMetadata? metadata = null)
        : base(reference, metadata)
    {
    }
}
