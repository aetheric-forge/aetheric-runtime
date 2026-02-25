namespace AethericForge.Runtime.Tests;

using Newtonsoft.Json;

using AethericForge.Runtime.Repo.Abstractions;

public static class TestModels
{
    public class TestMessage : IEntity
    {
        public Guid Id { get; init; }
        public string Message { get; init; }

        public TestMessage(string message)
        {
            Id = Guid.NewGuid();
            Message = message;
        }

        [JsonConstructor]
        public TestMessage(Guid id, string message)
        {
            Id = id;
            Message = message;
        }
    }

    public class MessageSent : IEntity
    {
        public Guid Id { get; init; }

        public MessageSent(Guid id)
        {
            Id = id;
        }
    }
}
