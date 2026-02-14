namespace AethericForge.Runtime.Model.Threads;

public sealed class Story
(
    string? id,
    string sagaId,
    string title,
    int priority,
    string quantum,
    EnergyBand energy = EnergyBand.Moderate,
    StoryState state = StoryState.Ready,
    bool archived = false,
    DateTime? dueAt = null,
    DateTime? createdAt = null,
    DateTime? updatedAt = null,
    DateTime? archivedAt = null
) : Thread(id, title, priority, quantum, archived, createdAt, updatedAt, archivedAt)
{
    public string SagaId { get; set; } = sagaId;
    public DateTime? DueAt { get; set; } = dueAt;
    // Use auto-properties for consistency with the rest of the model
    public EnergyBand Energy { get; set; } = energy;
    public StoryState StoryState { get; set; } = state;

    public override Thread Clone() =>
        new Story(
            id: Id,
            sagaId: SagaId,
            title: Title,
            priority: Priority,
            quantum: Quantum,
            energy: Energy,
            state: StoryState,
            archived: Archived,
            dueAt: DueAt,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt,
            archivedAt: ArchivedAt
        );
}
