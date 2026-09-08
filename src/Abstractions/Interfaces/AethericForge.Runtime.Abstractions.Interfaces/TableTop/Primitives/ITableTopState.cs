namespace AethericForge.Runtime.Abstractions.Interfaces.TableTop.Primitives;

public interface ITableTopState
{
    ITableTopReference TableTop { get; }
    string Name { get; }
    string? ActiveSceneId { get; }
    Uri? JoinUrl { get; }
}
