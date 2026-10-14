using System.Collections.Immutable;
using System.Globalization;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Keycloak;

namespace Aetheric.Provisioning.Worker;

public sealed record DecisionsConsumerOptions(string OrganizationId, string DisplayName, string MemberGroupId, string MaintainerGroupId);

/// <summary>Exports only scoped resource credentials from successful, catalog-owned deployments.</summary>
public sealed class WebModuleConsumerProvisioner(ISecretStore secrets, IConsumerConfigurationStore configurations,
    IInstitutionDeploymentStore deployments, IArchiveConsumerCredentials archiveCredentials,
    DecisionsConsumerOptions? decisionsOptions = null, Uri? publicOrigin = null, Uri? registryAuthority = null,
    Func<RootCredential, KeycloakResourceProvider>? registryFactory = null)
{
    public static bool Supports(string id) => id is "library" or "archive" or "decisions";

    public async Task<ConsumerDelivery> ProvisionAsync(InstitutionDeploymentAttempt deployment,
        IReadOnlyDictionary<string, RootCredentialPayload> roots, CancellationToken ct, DecisionsConsumerOptions? organization = null)
    {
        if (deployment.Status != InstitutionDeploymentStatus.Succeeded || !Supports(deployment.InstitutionId))
            throw new ConsumerProvisioningException("consumer.successful_deployment_required");
        var office = organization ?? decisionsOptions;
        var module = deployment.InstitutionId;
        var section = WebModuleConsumerProfiles.Section(module);
        var settings = new Dictionary<string, string> { [section + ":Enabled"] = "true" };
        var references = new Dictionary<string, SecretReference>();
        var chain = new List<InstitutionDeploymentAttempt> { deployment };
        if (module == "decisions")
        {
            if (office is null || new[] { office.OrganizationId, office.DisplayName,
                    office.MemberGroupId, office.MaintainerGroupId }.Any(string.IsNullOrWhiteSpace))
                throw new ConsumerProvisioningException("consumer.organization_configuration_required");
            var parentId = deployment.Parent?.DeploymentId
                ?? throw new ConsumerProvisioningException("consumer.catalog_owner_required");
            var visited = new HashSet<string> { deployment.DeploymentId };
            while (parentId is not null)
            {
                if (!visited.Add(parentId) || visited.Count > 64)
                    throw new ConsumerProvisioningException("consumer.invalid_owner_chain");
                var ancestor = (await deployments.ReadAsync(parentId, ct))?.Provisioned;
                if (ancestor is null || ancestor.Environment != deployment.Environment)
                    throw new ConsumerProvisioningException("consumer.owner_environment_mismatch");
                chain.Add(ancestor);
                parentId = ancestor.Parent?.DeploymentId;
            }
            settings["Decisions:Owner:DeploymentId"] = chain[1].DeploymentId;
            settings["Decisions:Owner:Environment"] = deployment.Environment;
            settings["Organization:Id"] = office.OrganizationId;
            settings["Organization:DisplayName"] = office.DisplayName;
            settings["Organization:MemberGroupId"] = office.MemberGroupId;
            settings["Organization:MaintainerGroupId"] = office.MaintainerGroupId;
        }

        (InstitutionDeploymentAttempt Owner, ProvisionedResource Resource) Resource(string category, string provider)
        {
            foreach (var owner in chain)
            {
                var candidates = owner.Resources.Values.Where(r => r.Category == category && r.Provider == provider).ToArray();
                if (candidates.Length == 1) return (owner, candidates[0]);
                if (candidates.Length > 1) throw new ConsumerProvisioningException("consumer.ambiguous_resource");
            }
            throw new ConsumerProvisioningException("consumer.owner_resource_missing");
        }
        async Task<string> Password(ProvisionedResource resource)
        {
            if (resource.Secrets.Length != 1) throw new ConsumerProvisioningException("consumer.scoped_credentials_missing");
            references[resource.Provider] = resource.Secrets[0];
            return await secrets.ReadAsync(resource.Secrets[0], ct);
        }
        string Connection(ProvisionedResource resource, string key) => resource.Connection.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value) ? value : throw new ConsumerProvisioningException("consumer.resource_connection_missing");

        if (module is "library" or "decisions")
        {
            var (_, resource) = Resource("knowledge", "mongodb");
            foreach (var key in new[] { "Host", "Port", "Username", "AuthenticationDatabase" })
                settings[section + ":MongoDb:" + key] = Connection(resource, key);
            settings[section + ":MongoDb:DatabaseName"] = resource.Settings["database"];
            settings[section + ":MongoDb:Password"] = await Password(resource);
        }
        if (module is "archive" or "decisions")
        {
            var (_, resource) = Resource("archive", "s3");
            if (!roots.TryGetValue("s3", out var root)) throw new ConsumerProvisioningException("consumer.archive_root_required");
            var accessKey = "web" + deployment.DeploymentId[..24];
            var reference = await secrets.GetOrCreateAsync("s3-consumer", deployment.DeploymentId, ct);
            var secret = await secrets.ReadAsync(reference, ct);
            await archiveCredentials.EnsureAsync(root, accessKey, secret, resource.Settings["bucket"], ct);
            references["S3"] = reference;
            settings[section + ":S3:ServiceUrl"] = new UriBuilder(root.Scheme ?? "https", root.Host, root.Port).Uri.AbsoluteUri;
            settings[section + ":S3:AccessKey"] = accessKey;
            settings[section + ":S3:SecretKey"] = secret;
            settings[section + ":S3:BucketName"] = resource.Settings["bucket"];
            settings[section + ":S3:ForcePathStyle"] = "true";
            settings[section + ":S3:AuthenticationRegion"] = "us-east-1";
        }
        if (module == "decisions")
        {
            _ = Resource("staging", "workbench");
            var (postOwner, post) = Resource("post", "rabbitmq");
            if (!roots.TryGetValue("rabbitmq", out var broker)) throw new ConsumerProvisioningException("consumer.post_root_required");
            // RabbitMQ root carries the management API port. Runtime clients use AMQP, not that port.
            settings["Decisions:RabbitMq:Host"] = Connection(post, "Host");
            settings["Decisions:RabbitMq:Port"] = broker.Scheme == "https" ? "5671" : "5672";
            settings["Decisions:RabbitMq:Ssl"] = (broker.Scheme == "https").ToString();
            settings["Decisions:RabbitMq:Username"] = postOwner.InstitutionId + "-" + post.Id;
            settings["Decisions:RabbitMq:Password"] = await Password(post);
            settings["Decisions:RabbitMq:VirtualHost"] = post.Settings["vhost"];
            var (_, registry) = Resource("identity", "keycloak");
            if (publicOrigin is null || registryAuthority is null || !roots.TryGetValue("keycloak", out var keycloak))
                throw new ConsumerProvisioningException("consumer.registry_origin_required");
            var client = "decisions-directory-" + deployment.DeploymentId[..24];
            var reference = await secrets.GetOrCreateAsync("oidc-directory", deployment.DeploymentId, ct);
            var secret = await secrets.ReadAsync(reference, ct);
            using var provider = registryFactory?.Invoke(new RootCredential(keycloak.Host, keycloak.Port, keycloak.Username, keycloak.Password)
                { Keycloak = new(keycloak.Scheme ?? "https", keycloak.BasePath ?? "/", keycloak.Realm ?? "master") })
                ?? new KeycloakResourceProvider(new RootCredential(keycloak.Host, keycloak.Port, keycloak.Username, keycloak.Password)
                    { Keycloak = new(keycloak.Scheme ?? "https", keycloak.BasePath ?? "/", keycloak.Realm ?? "master") });
            await provider.EnsureBrowserClientAsync(registry.Settings["realm"], client, deployment.DeploymentId, secret, publicOrigin, ct, directoryReader: true);
            references["Directory"] = reference;
            settings["Decisions:Directory:Keycloak:Authority"] = registryAuthority.AbsoluteUri.TrimEnd('/');
            settings["Decisions:Directory:Keycloak:Realm"] = registry.Settings["realm"];
            settings["Decisions:Directory:Keycloak:ClientId"] = client;
            settings["Decisions:Directory:Keycloak:ClientSecret"] = secret;
        }
        if (!WebModuleConsumerProfiles.RequiredSettings(module).SetEquals(settings.Keys))
            throw new ConsumerProvisioningException("consumer.incomplete_configuration");
        await configurations.WriteAsync(new ConsumerConfiguration(1, module, deployment.DeploymentId, module,
            deployment.Version, deployment.Environment, deployment.PlanId, settings.ToImmutableDictionary())
            { InstitutionName = deployment.Name, Description = deployment.Description }, ct);
        return new(ConsumerDeliveryStatus.Ready, module, deployment.PlanId, references.ToImmutableDictionary(), []);
    }
}
