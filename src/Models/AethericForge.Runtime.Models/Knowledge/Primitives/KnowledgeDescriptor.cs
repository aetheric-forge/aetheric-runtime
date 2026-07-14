using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Models.Knowledge.Primitives;

public sealed record KnowledgeDescriptor : IKnowledgeDescriptor
{
    public KnowledgeDescriptor(
        string title,
        string? @abstract = null,
        string? summary = null,
        string? description = null)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Abstract = @abstract;
        Summary = summary;
        Description = description;
    }

    public string Title { get; init; }
    public string? Abstract { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
}
