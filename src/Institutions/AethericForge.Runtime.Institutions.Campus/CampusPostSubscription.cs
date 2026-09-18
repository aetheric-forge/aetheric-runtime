using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AethericForge.Runtime.Institutions.Campus;

/// <summary>
/// One (reference, consumer) pair an app wants actively subscribed for the lifetime of the host.
/// </summary>
public sealed record PostSubscriptionRequest(IPostReference Reference, IMessageConsumer Consumer);

/// <summary>
/// IPostProvider.SubscribeAsync (e.g. RabbitMqPostProvider's AsyncEventingBasicConsumer-backed
/// consumer loop) already does the real listening work once subscribed - nothing in any app calls
/// SubscribeAsync in production today, so a composed campus's Post Office is publish-only in
/// practice. This hosted service calls it once, at host startup, for every
/// PostSubscriptionRequest an app registered via AddPostSubscription - it is a lifetime wrapper
/// and a registration point, not new consumer infrastructure.
///
/// Works at the IPostProvider level (mirroring PostService.SubscribeAsync's own domain-routing)
/// rather than through IPostService, since IPostService.SubscribeAsync&lt;TMessage&gt; requires a
/// generic IMessageConsumer&lt;TMessage&gt; per call, but a campus can subscribe to several
/// message types across several references - IPostProvider.SubscribeAsync accepts the
/// non-generic IMessageConsumer base directly, so one hosted service can carry all of them.
/// </summary>
public sealed class PostOfficeSubscriptionHostedService(
    IEnumerable<IPostProvider> providers,
    IEnumerable<PostSubscriptionRequest> subscriptions) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var providersByName = providers.ToDictionary(provider => provider.Name, StringComparer.Ordinal);
        foreach (var subscription in subscriptions)
        {
            if (!providersByName.TryGetValue(subscription.Reference.Domain, out var provider))
            {
                throw new KeyNotFoundException(
                    $"No post provider is registered for domain '{subscription.Reference.Domain}'.");
            }

            await provider.SubscribeAsync(subscription.Reference, subscription.Consumer, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class CampusPostSubscriptionExtensions
{
    /// <summary>
    /// Registers a subscription to be activated at host startup. Safe to call more than once -
    /// PostOfficeSubscriptionHostedService is only ever added to IHostedService once
    /// (TryAddEnumerable), regardless of how many subscriptions are registered, so every
    /// subscription gets started exactly once rather than once per registered subscription.
    /// </summary>
    public static IServiceCollection AddPostSubscription(
        this IServiceCollection services,
        IPostReference reference,
        IMessageConsumer consumer)
    {
        services.AddSingleton(new PostSubscriptionRequest(reference, consumer));
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, PostOfficeSubscriptionHostedService>());
        return services;
    }
}
