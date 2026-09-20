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
// TODO(Stage 6): generate and persist this key properly (matching ManagedRootCredentialStore's
// own key-file convention) once a resource provider actually generates a secret worth keeping
// across restarts. Ephemeral for now: a fresh random key every start, with any secret files left
// over from a previous run's (different) key wiped first - EncryptedFileSecretStore only expects
// FileNotFoundException from a missing secret, not a decrypt failure from a stale key, so leaving
// old ciphertext in place would crash EnsureAsync instead of cleanly rotating it.
if (Directory.Exists(secretsDirectory)) Directory.Delete(secretsDirectory, recursive: true);
var secretKey = new byte[32];
RandomNumberGenerator.Fill(secretKey);

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
