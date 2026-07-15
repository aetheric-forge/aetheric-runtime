namespace AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

public interface IPostReference
{
    string Domain { get; }
    string Address { get; }
    IPostContract Contract { get; }
    IReadOnlyDictionary<string, string> Qualifiers { get; }
}
