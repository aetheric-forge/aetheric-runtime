using System.Collections.Immutable;
using System.Globalization;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Keycloak;
using StackExchange.Redis;

namespace Aetheric.Provisioning.Worker;

/// <summary>Materializes a Parallel You consumer after its institution plan succeeds.</summary>
public sealed class ParallelYouConsumerProvisioner(ISecretStore secrets, IConsumerConfigurationStore configurations,
    Uri publicOrigin, Func<RootCredential, KeycloakResourceProvider>? registryFactory = null, Uri? registryAuthority = null)
{
    internal static readonly string[] Stages = ["ReflectionMapping", "TrackingCurrent", "IntentionCurrent", "PlanCurrent", "RecommendationCurrent"];

    public async Task<ConsumerDelivery> ProvisionAsync(InstitutionDeploymentAttempt deployment,
        IReadOnlyDictionary<string, RootCredentialPayload> roots, CancellationToken ct)
    {
        if (!publicOrigin.IsAbsoluteUri || publicOrigin.UserInfo.Length != 0 || publicOrigin.Query.Length != 0
            || publicOrigin.Fragment.Length != 0 || publicOrigin.AbsolutePath != "/"
            || (publicOrigin.Scheme != "https" && !(publicOrigin.Scheme == "http" && publicOrigin.IsLoopback)))
            throw new ArgumentException("An exact HTTPS public origin or loopback development origin is required.");
        if (registryAuthority is not null && (!registryAuthority.IsAbsoluteUri || registryAuthority.UserInfo.Length != 0
            || registryAuthority.Query.Length != 0 || registryAuthority.Fragment.Length != 0
            || (registryAuthority.Scheme != "https" && !(registryAuthority.Scheme == "http" && registryAuthority.IsLoopback))))
            throw new ArgumentException("A public HTTPS registry authority or loopback development authority is required.");
        if (deployment.Status != InstitutionDeploymentStatus.Succeeded || deployment.InstitutionId != "parallel-you"
            || !deployment.Resources.TryGetValue("library", out var library) || library.Provider != "mongodb"
            || library.Secrets.Length != 1 || !library.Settings.TryGetValue("database", out var database)
            || deployment.Parent is null || !deployment.Parent.ResourceLocations.TryGetValue("IRegistrar", out var realm)
            || !deployment.Parent.ResourceLocations.ContainsKey("IWorkbench"))
            throw new InvalidOperationException("Parallel You requires a provisioned Library, Registrar, and Workbench.");
        var mongo = roots["mongo"];
        var redis = roots["redis"];
        var keycloak = roots["keycloak"];
        var suffix = deployment.DeploymentId[..24];
        var redisUser = "parallel-you-" + suffix;
        var browserClient = "parallel-you-web-" + suffix;
        var redisReference = await secrets.GetOrCreateAsync("redis-consumer", deployment.DeploymentId, ct);
        var redisPassword = await secrets.ReadAsync(redisReference, ct);
        var clientReference = await secrets.GetOrCreateAsync("oidc-consumer", deployment.DeploymentId, ct);
        var clientSecret = await secrets.ReadAsync(clientReference, ct);
        var redisOptions = new ConfigurationOptions
        {
            User = redis.Username, Password = redis.Password, Ssl = redis.Scheme == "rediss",
            DefaultDatabase = int.Parse(redis.Database ?? "0", CultureInfo.InvariantCulture),
            AbortOnConnectFail = true
        };
        redisOptions.EndPoints.Add(redis.Host, redis.Port);
        using (var connection = await ConnectionMultiplexer.ConnectAsync(redisOptions))
        {
            ct.ThrowIfCancellationRequested();
            await connection.GetDatabase().ExecuteAsync("ACL", BuildAclArguments(redisUser, redisPassword));
            try { await connection.GetDatabase().ExecuteAsync("ACL", "SAVE"); }
            catch (RedisServerException)
            { throw new ConsumerProvisioningException("consumer.redis_acl_persistence_required"); }
        }
        var registryRoot = new RootCredential(keycloak.Host, keycloak.Port, keycloak.Username, keycloak.Password)
        {
            Keycloak = new KeycloakRootOptions(keycloak.Scheme ?? "https", keycloak.BasePath ?? "/", keycloak.Realm ?? "master")
        };
        using (var registry = registryFactory?.Invoke(registryRoot) ?? new KeycloakResourceProvider(registryRoot))
            await registry.EnsureBrowserClientAsync(realm, browserClient, deployment.DeploymentId, clientSecret, publicOrigin, ct);

        var authority = registryAuthority?.AbsoluteUri.TrimEnd('/') ?? new UriBuilder(keycloak.Scheme ?? "https", keycloak.Host, keycloak.Port, keycloak.BasePath ?? "/").Uri.AbsoluteUri.TrimEnd('/');
        var settings = new Dictionary<string, string>
        {
            ["ParallelYou:Enabled"] = "true",
            ["ParallelYou:MongoDb:Host"] = mongo.Host,
            ["ParallelYou:MongoDb:Port"] = mongo.Port.ToString(CultureInfo.InvariantCulture),
            ["ParallelYou:MongoDb:DatabaseName"] = database,
            ["ParallelYou:MongoDb:Username"] = deployment.InstitutionId + "-library",
            ["ParallelYou:MongoDb:Password"] = await secrets.ReadAsync(library.Secrets[0], ct),
            ["ParallelYou:MongoDb:AuthenticationDatabase"] = "admin",
            ["Workbench:Redis:Host"] = redis.Host,
            ["Workbench:Redis:Port"] = redis.Port.ToString(CultureInfo.InvariantCulture),
            ["Workbench:Redis:Ssl"] = (redis.Scheme == "rediss").ToString(),
            ["Workbench:Redis:Database"] = redis.Database ?? "0",
            ["Workbench:Redis:User"] = redisUser,
            ["Workbench:Redis:Password"] = redisPassword,
            ["Registry:Keycloak:Authority"] = authority,
            ["Registry:Keycloak:Realm"] = realm,
            ["Registry:Keycloak:ClientId"] = browserClient,
            ["Registry:Keycloak:ClientSecret"] = clientSecret
        };
        await configurations.WriteAsync(new ConsumerConfiguration(1, "parallel-you", deployment.DeploymentId,
            deployment.InstitutionId, deployment.Version, deployment.Environment, deployment.PlanId, settings.ToImmutableDictionary())
        { InstitutionName = deployment.Name, Description = deployment.Description }, ct);
        return new ConsumerDelivery(ConsumerDeliveryStatus.Ready, "parallel-you", deployment.PlanId,
            new Dictionary<string, SecretReference> { ["Redis"] = redisReference, ["Oidc"] = clientReference }.ToImmutableDictionary(), []);
    }

    internal static object[] BuildAclArguments(string username, string password) =>
        new object[] { "SETUSER", username, "reset", "on", ">" + password,
            "+hset", "+hmset", "+hget", "+hgetall", "+hmget", "+exists", "+del", "+unlink", "+expire", "+pexpire", "+persist", "+set", "+get",
            "+pttl", "+ttl", "+watch", "+unwatch", "+multi", "+exec", "+discard", "+info", "+ping", "+hello",
            "+echo", "+select", "+client|id", "+client|setname", "+client|setinfo", "+eval", "+evalsha", "+script|load" }
        .Concat(Stages.SelectMany(stage => new object[] { "~" + stage + ":data:*", "~" + stage + ":lock:*" })).ToArray();
}

public sealed class ConsumerProvisioningException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
