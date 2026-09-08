namespace AethericForge.Runtime.Abstractions.Interfaces.TableTop.Primitives;

public enum TableTopEventKind { Connected, Disconnected, ActiveSceneChanged }

public interface ITableTopEvent
{
    string EventId { get; }
    ITableTopReference TableTop { get; }
    TableTopEventKind Kind { get; }
    DateTimeOffset OccurredAt { get; }
    string? ActiveSceneId { get; }
    string? OriginOperationId { get; }
}
