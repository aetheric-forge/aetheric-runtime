namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;

public interface IExternalIdentityChange
{
    IExternalIdentity? Previous { get; }
    IExternalIdentity Current { get; }
    IReadOnlyCollection<string> ChangedProperties { get; }
    DateTimeOffset ObservedAtUtc { get; }
}
