using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Keycloak;
using Aetheric.Provisioning.Persistence;
using Aetheric.Provisioning.Worker;
using AethericForge.Runtime.Models.Staging;
using AethericForge.Runtime.Providers.Staging.Redis;
using StackExchange.Redis;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class ParallelYouConsumerTests
{
    [ConsumerRedisFact]
    public async Task Consumer_export_uses_scoped_credentials_and_redis_grants_cannot_touch_other_modules()
    {
        var directory = Path.Combine(Path.GetTempPath(), "parallel-you-consumer-" + Guid.NewGuid().ToString("N"));
        var uri = new Uri(Environment.GetEnvironmentVariable("PROVISIONING_CONSUMER_TEST_REDIS")!);
        var redisRootPassword = Uri.UnescapeDataString(uri.UserInfo.Split(':', 2)[1]);
        var roots = new Dictionary<string, RootCredentialPayload>
        {
            ["redis"] = new(uri.Host, uri.Port, "default", redisRootPassword),
            ["mongo"] = new("mongo.example", 27017, "mongo-root", "mongo-root-secret"),
            ["keycloak"] = new("identity.example", 443, "keycloak-root", "keycloak-root-secret")
        };
        var secrets = new EncryptedFileSecretStore(Path.Combine(directory, "secrets"), RandomNumberGenerator.GetBytes(32));
        var librarySecret = await secrets.GetOrCreateAsync("mongodb", "parallel-you-library", default);
        var source = new SourceProvenance("https://github.com/aetheric-forge/aetheric-web", new string('a', 40), "institution.yaml", new string('b', 64));
        var deploymentId = InstitutionDeploymentAttempt.Identity(source.Repository, "parallel-you", "test");
        var library = new ProvisionedResource("library", "knowledge", "mongodb",
            new Dictionary<string, string> { ["database"] = "parallel-you" }.ToImmutableDictionary(), [librarySecret]);
        var deployment = new InstitutionDeploymentAttempt(deploymentId, Guid.NewGuid(), "parallel-you", "My Parallel You", "Private practice", "1.0.0", "test",
            new DeploymentParent("https://example.com/campus", new string('c', 40), new string('d', 64),
                new Dictionary<string, string> { ["IRegistrar"] = "members", ["IWorkbench"] = "verified-parent-workbench" }.ToImmutableDictionary()),
            source, source, new string('e', 64), InstitutionDeploymentStatus.Succeeded, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            new Dictionary<string, ProvisionedResource> { ["library"] = library }.ToImmutableDictionary(), [], []);
        var provisioner = new ParallelYouConsumerProvisioner(secrets, new FileConsumerConfigurationStore(Path.Combine(directory, "consumers")),
            new Uri("https://forge.example/"), root => new KeycloakResourceProvider(root, new BrowserClientHandler()), registryAuthority: new Uri("https://public-identity.example/"));
        var key = "consumer-test-" + Guid.NewGuid().ToString("N");
        string? scopedUser = null;
        try
        {
            var delivery = await provisioner.ProvisionAsync(deployment, roots, default);
            Assert.Equal(ConsumerDeliveryStatus.Ready, delivery.Status);
            var path = Path.Combine(directory, "consumers", deploymentId, "configuration.json");
            var bytes = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain(redisRootPassword, bytes);
            Assert.DoesNotContain("mongo-root-secret", bytes);
            Assert.DoesNotContain("keycloak-root-secret", bytes);
            var manifest = JsonSerializer.Deserialize<ConsumerConfiguration>(bytes)!;
            Assert.Equal("parallel-you-library", manifest.Settings["ParallelYou:MongoDb:Username"]);
            Assert.Equal("https://public-identity.example", manifest.Settings["Registry:Keycloak:Authority"]);
            Assert.Equal("members", manifest.Settings["Registry:Keycloak:Realm"]);
            Assert.Equal("admin", manifest.Settings["ParallelYou:MongoDb:AuthenticationDatabase"]);
            Assert.Equal("My Parallel You", manifest.InstitutionName);
            scopedUser = manifest.Settings["Workbench:Redis:User"];
            using (var isolatedRoot = await ConnectionMultiplexer.ConnectAsync($"{uri.Host}:{uri.Port},password={redisRootPassword}"))
                await isolatedRoot.GetDatabase().ExecuteAsync("ACL", "LOAD"); // Proves the grant survives reload from its persistent ACL file.
            var options = new ConfigurationOptions
            {
                User = scopedUser, Password = manifest.Settings["Workbench:Redis:Password"],
                AbortOnConnectFail = true, ConfigurationChannel = "", TieBreaker = "",
                CommandMap = CommandMap.Create(new HashSet<string> { "CONFIG", "CLUSTER" }, available: false)
            };
            options.EndPoints.Add(uri.Host, uri.Port);
            using var consumer = await ConnectionMultiplexer.ConnectAsync(options);
            var staging = new RedisStagingProvider(consumer, "PlanCurrent");
            await using var content = new MemoryStream(new byte[] { 1, 2, 3 });
            var reference = await staging.PutAsync(key, content);
            Assert.True(await staging.ExistsAsync(reference));
            await using var retrieved = await staging.OpenReadAsync(reference);
            Assert.Equal(3, retrieved.Length);
            await using (var lease = await staging.AcquireLockAsync(reference)) Assert.True(lease.IsAcquired);
            Assert.True(await staging.DeleteAsync(reference));
            await Assert.ThrowsAsync<RedisServerException>(() => consumer.GetDatabase().StringSetAsync("Decisions:data:" + key, "forbidden"));
            await Assert.ThrowsAsync<RedisServerException>(() => consumer.GetDatabase().ExecuteAsync("FLUSHDB"));
            Assert.DoesNotContain(redisRootPassword, await secrets.ReadAsync(delivery.Credentials["Redis"], default));
        }
        finally
        {
            using var root = await ConnectionMultiplexer.ConnectAsync($"{uri.Host}:{uri.Port},password={redisRootPassword}");
            await root.GetDatabase().KeyDeleteAsync(new RedisKey[] { "PlanCurrent:data:" + key, "PlanCurrent:lock:" + key });
            if (scopedUser is not null)
            {
                await root.GetDatabase().ExecuteAsync("ACL", "DELUSER", scopedUser);
                await root.GetDatabase().ExecuteAsync("ACL", "SAVE");
            }
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

public sealed class ConsumerRedisFactAttribute : FactAttribute
{
    public ConsumerRedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PROVISIONING_CONSUMER_TEST_REDIS")))
            Skip = "Set PROVISIONING_CONSUMER_TEST_REDIS to an isolated Redis root URI.";
    }
}
