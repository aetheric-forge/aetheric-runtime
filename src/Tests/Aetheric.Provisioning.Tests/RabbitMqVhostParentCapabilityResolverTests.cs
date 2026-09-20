using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.RabbitMq;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class RabbitMqVhostParentCapabilityResolverTests
{
    private static ParentContext Parent => new("https://example.test/fixture", "0000000000000000000000000000000000000000");

    [RabbitMqFact]
    public async Task Unrecognized_contracts_are_always_unavailable()
    {
        using var resolver = new RabbitMqVhostParentCapabilityResolver(ManagementCredential(), "/");
        Assert.False(await resolver.IsAvailableAsync(Parent, "ISomethingElse", "campus.post-office", default));
    }

    [RabbitMqFact]
    public async Task The_default_vhost_is_available()
    {
        using var resolver = new RabbitMqVhostParentCapabilityResolver(ManagementCredential(), "/");
        Assert.True(await resolver.IsAvailableAsync(Parent, "IPostOffice", "campus.post-office", default));
    }

    [RabbitMqFact]
    public async Task A_vhost_that_was_never_created_is_unavailable()
    {
        using var resolver = new RabbitMqVhostParentCapabilityResolver(ManagementCredential(), "nonexistent-" + Guid.NewGuid().ToString("N"));
        Assert.False(await resolver.IsAvailableAsync(Parent, "IPostOffice", "campus.post-office", default));
    }

    private static RootCredential ManagementCredential()
    {
        var amqp = new Uri(Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!);
        var userInfo = amqp.UserInfo.Split(':', 2);
        return new RootCredential(amqp.Host, 15672, Uri.UnescapeDataString(userInfo[0]), Uri.UnescapeDataString(userInfo[1]));
    }
}
