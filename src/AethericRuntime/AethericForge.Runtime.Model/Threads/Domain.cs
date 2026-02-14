namespace AethericForge.Runtime.Model.Threads;

public sealed class Domain
(
    string? id,
    string title,
    int priority,
    string quantum,
    string? description = null,
    bool archived = false,
    DateTime? createdAt = null,
    DateTime? updatedAt = null,
    DateTime? archivedAt = null
) : Thread(id, title, priority, quantum, archived, createdAt, updatedAt, archivedAt)
{
    public string? Description { get; set; } = description;

    public override Thread Clone() =>
        new Domain(
            id: Id,
            title: Title,
            priority: Priority,
            quantum: Quantum,
            description: Description,
            archived: Archived,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt,
            archivedAt: ArchivedAt
        );
}
