using AethericForge.Runtime.Util;

namespace AethericForge.Runtime.Model.Threads;

public abstract class Thread
(
    string? id,
    string title,
    int priority,
    string quantum,
    bool archived = false,
    DateTime? createdAt = null,
    DateTime? updatedAt = null,
    DateTime? archivedAt = null
)
{
    public string Id { get; } = string.IsNullOrWhiteSpace(id) ? Slug.From(title) : id;
    public string Title { get; set; } = title;
    public int Priority  { get; set; } = priority;
    public string Quantum { get; set; } = quantum;
    public bool Archived { get; set; } = archived;
    public DateTime? CreatedAt { get; set; } = createdAt;
    public DateTime? UpdatedAt { get; set; } = updatedAt;
    public DateTime? ArchivedAt { get; set; } = archivedAt;
    public abstract Thread Clone();
}
