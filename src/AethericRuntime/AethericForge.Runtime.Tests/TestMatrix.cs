using AethericForge.Runtime.Bus;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Bus.Transports;
using AethericForge.Runtime.Repo;
using AethericForge.Runtime.Repo.Abstractions;

namespace AethericForge.Runtime.Tests;

public static class TestMatrix
{
    // Provides transports/brokers to test: always InMemory; add RabbitMQ when RABBITMQ_URL is set
    public static IEnumerable<object[]> BusCases()
    {
        // InMemory case
        yield return new object[]
        {
            (Func<(ITransport transport, IBroker broker)>)(() =>
            {
                var t = new InMemoryTransport();
                IBroker b = new MessageBroker(t);
                return (t, b);
            })
        };

        // RabbitMQ case (optional)
        var rabbitUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL");
        if (!string.IsNullOrWhiteSpace(rabbitUrl))
        {
            yield return new object[]
            {
                (Func<(ITransport transport, IBroker broker)>)(() =>
                {
                    var t = new RabbitMqTransport(rabbitUrl, "aetheric-tests");
                    IBroker b = new MessageBroker(t);
                    return (t, b);
                })
            };
        }
    }

    // Provides repos to test: always InMemory; add Mongo when MONGO_URI is set and MongoRepo type is available
    public static IEnumerable<object[]> RepoCases()
    {
        // InMemory case
        yield return new object[]
        {
            (Func<IRepo<TestModels.TestMessage>>)(() => new InMemoryRepo<TestModels.TestMessage>())
        };

        var mongoUri = Environment.GetEnvironmentVariable("MONGO_URI");
        if (!string.IsNullOrWhiteSpace(mongoUri))
        {
            // Try to find MongoRepo type via reflection to avoid compile issues if backend isn't present
            var type = Type.GetType("AethericForge.Runtime.Repo.Backends.MongoRepo, AethericForge.Runtime.Repo");
            if (type != null)
            {
                var ctor = type.GetConstructor(new[] { typeof(string), typeof(string), typeof(string) });
                if (ctor != null)
                {
                    yield return new object[]
                    {
                        () =>
                        {
                            // database/collection names defaulted as in app Program.cs
                            return ctor.Invoke(new[] { mongoUri, "parallel_you", "threads" });
                        }
                    };
                }
            }
        }
    }
}
