using System.Security.Cryptography;
using Aetheric.Provisioning.Application;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Persistence;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Institutions.Campus;
using AethericForge.Runtime.Providers.Post.RabbitMq;
using Aetheric.Provisioning.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Built directly rather than resolved through DI: AddPostSubscription needs a concrete consumer
// instance up front, before the container exists to resolve one from.
var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, BuildRabbitMqUrl(builder.Configuration));
builder.Services.AddSingleton<IPostProvider>(postProvider);

var definitionSource = new PublicGitHubSource(PublicGitHubSource.CreateHttpClient());
var reader = new InstitutionYamlReader();
var dataDirectory = builder.Configuration["Provisioning:DataDirectory"] ?? Path.Combine(AppContext.BaseDirectory, "data");
var secretsDirectory = Path.Combine(dataDirectory, "secrets");
var secretsKeyDirectory = builder.Configuration["Provisioning:SecretsKeyDirectory"] ?? Path.Combine(dataDirectory, "secrets-key");
var secretKey = await ResolveSecretKeyAsync(secretsDirectory, secretsKeyDirectory);

var runStateStore = new FileRunStateStore(Path.Combine(dataDirectory, "run-state"));
var secretStore = new EncryptedFileSecretStore(secretsDirectory, secretKey);

builder.Services.AddPostSubscription(
    ProvisioningPost.RequestReference(),
    new InstitutionDeploymentRequestConsumer(postProvider, definitionSource, reader, runStateStore, secretStore));

builder.Services.AddPostSubscription(
    ProvisioningBootstrapPost.RequestReference(),
    new InstitutionBootstrapRequestConsumer(postProvider, reader, runStateStore, secretStore));

var host = builder.Build();
await host.RunAsync();

static string BuildRabbitMqUrl(IConfiguration configuration)
{
    var useSsl = configuration.GetValue("RabbitMq:Ssl", false);
    var uriBuilder = new UriBuilder
    {
        Scheme = useSsl ? "amqps" : "amqp",
        Host = Required(configuration, "RabbitMq:Host"),
        Port = configuration.GetValue<int?>("RabbitMq:Port") ?? (useSsl ? 5671 : 5672),
        UserName = Required(configuration, "RabbitMq:Username"),
        Password = Required(configuration, "RabbitMq:Password"),
        Path = Uri.EscapeDataString(Required(configuration, "RabbitMq:VirtualHost"))
    };
    return uriBuilder.Uri.ToString();
}

static string Required(IConfiguration configuration, string key) =>
    !string.IsNullOrWhiteSpace(configuration[key])
        ? configuration[key]!
        : throw new InvalidOperationException($"{key} is required.");

// Mirrors Aetheric.Provisioning.Persistence.ManagedRootCredentialStore's own key-file convention
// (a separate key directory; a 32-byte key created once and reused; never silently regenerated
// once real secrets exist under it) rather than the previous "wipe secrets, fresh random key every
// start" behavior - RabbitMQ/MongoDB/Keycloak now all generate real scoped-credential secrets that
// ProvisioningEngine.ExecuteAsync's checkpoint-skip path reads back on a later run, so a fresh key
// on every restart was silently discarding already-used credentials, not just harmlessly rotating
// an unused one.
static async Task<byte[]> ResolveSecretKeyAsync(string secretsDirectory, string keyDirectory)
{
    Directory.CreateDirectory(keyDirectory);
    var keyPath = Path.Combine(keyDirectory, "secret-key.key");
    if (File.Exists(keyPath))
    {
        var key = await File.ReadAllBytesAsync(keyPath);
        if (key.Length != 32) throw new InvalidDataException("Invalid provisioning secrets encryption key.");
        return key;
    }
    if (Directory.Exists(secretsDirectory) && Directory.EnumerateFiles(secretsDirectory).Any())
        throw new InvalidDataException(
            "Provisioning secrets encryption key is missing but secret files already exist. Restore the original key, or clear the secrets directory to start fresh.");
    var generated = new byte[32];
    RandomNumberGenerator.Fill(generated);
    await File.WriteAllBytesAsync(keyPath, generated);
    return generated;
}
