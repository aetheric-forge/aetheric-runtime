using AethericForge.Runtime.Bus;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Bus.Transports;
using AethericForge.Runtime.Repo;
using AethericForge.Runtime.Repo.Abstractions;
using System.Runtime.InteropServices;

namespace AethericForge.Runtime.Tests;

public static class TestMatrix
{
    // Provides transports/brokers to test: always InMemory; add RabbitMQ when RABBITMQ_URL is set
    public static IEnumerable<object[]> BusCases()
    {
        // InMemory case
        yield return new object[]
        {
            (Func<IBroker>)(() =>
            {
                var t = new InMemoryTransport();
                return new MessageBroker(t);
            })
        };

        // RabbitMQ case (optional)
        var rabbitUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL");
        if (!string.IsNullOrWhiteSpace(rabbitUrl))
        {
            yield return new object[]
            {
                (Func<IBroker>)(() =>
                {
                    var t = new RabbitMqTransport(rabbitUrl, "aetheric-tests");
                    return new MessageBroker(t);
                })
            };
        }
    }

    public static IEnumerable<object[]> HostTransportCases()
    {
        yield return new object[]
        {
            (Func<ITransport>)(() => new InMemoryTransport())
        };
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
            yield return new object[]
            {
                (Func<IRepo<TestModels.TestMessage>>)(() =>
                    MongoRepoFactory.Create<TestModels.TestMessage>(mongoUri, "forge", "tests", true)
                )
            };
        }
    }
}
