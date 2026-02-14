namespace AethericForge.Runtime.Model.Threads;

public sealed class Saga
(
    string? id,
    string domainId,
    string title,
    int priority,
    string quantum,
    bool archived = false,
    DateTime? createdAt = null,
    DateTime? updatedAt = null,
    DateTime? archivedAt = null
) : Thread(id, title, priority, quantum, archived, createdAt, updatedAt, archivedAt)
{
    public string DomainId { get; set; } = domainId;

    public override Thread Clone() =>
        new Saga(
            id: Id,
            domainId: DomainId,
            title: Title,
            priority: Priority,
            quantum: Quantum,
            archived: Archived,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt,
            archivedAt: ArchivedAt
        );
}
