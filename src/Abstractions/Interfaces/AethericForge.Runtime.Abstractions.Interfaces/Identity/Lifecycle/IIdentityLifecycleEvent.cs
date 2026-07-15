using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;

public interface IIdentityLifecycleEvent
{
    IIdentitySubject Subject { get; }
    DateTimeOffset Timestamp { get; }
    string EventType { get; }
    IReadOnlyDictionary<string, string> Data { get; }
}