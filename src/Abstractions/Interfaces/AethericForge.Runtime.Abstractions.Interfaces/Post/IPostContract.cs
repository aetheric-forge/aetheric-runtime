namespace AethericForge.Runtime.Abstractions.Interfaces.Post;

public interface IPostContract
{
    string Name { get; }
    string Version { get; }
    PostIntent Intent { get; }
}
