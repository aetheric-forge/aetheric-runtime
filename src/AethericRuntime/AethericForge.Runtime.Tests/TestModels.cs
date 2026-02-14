using AethericForge.Runtime.Model.Messages.Threads;
using AethericForge.Runtime.Model.Threads;

namespace AethericForge.Runtime.Tests;

public static class TestModels
{
    public static Domain Domain(
        string? id = null,
        string title = "Test Domain",
        int priority = 1,
        string quantum = "default",
        string? description = null,
        bool archived = false)
    {
        return new Domain(id, title, priority, quantum, description, archived);
    }

    public static Saga Saga(
        string? id = null,
        string domainId = "test-domain",
        string title = "Test Saga",
        int priority = 1,
        string quantum = "default",
        bool archived = false)
    {
        return new Saga(id, domainId, title, priority, quantum, archived);
    }

    public static Story Story(
        string? id = null,
        string sagaId = "test-saga",
        string title = "Test Story",
        int priority = 1,
        string quantum = "default",
        EnergyBand energy = EnergyBand.Moderate,
        StoryState state = StoryState.Ready,
        bool archived = false)
    {
        return new Story(id, sagaId, title, priority, quantum, energy, state, archived);
    }

    public static DomainCreated DomainCreated(Domain? domain = null) => new(domain ?? Domain());
    public static DomainUpdated DomainUpdated(Domain? domain = null) => new(domain ?? Domain());

    public static SagaCreated SagaCreated(Saga? saga = null) => new(saga ?? Saga());
    public static SagaUpdated SagaUpdated(Saga? saga = null) => new(saga ?? Saga());

    public static StoryCreated StoryCreated(Story? story = null) => new(story ?? Story());
    public static StoryUpdated StoryUpdated(Story? story = null) => new(story ?? Story());
}
