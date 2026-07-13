namespace AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

public interface IPostContract
{
    string Name { get; }
    string Version { get; }
    PostIntent Intent { get; }
}
