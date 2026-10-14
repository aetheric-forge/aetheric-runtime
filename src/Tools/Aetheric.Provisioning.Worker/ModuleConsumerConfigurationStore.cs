using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.Worker;

/// <summary>Routes each module to its own pre-created Secret; never shares another module's target.</summary>
public sealed class ModuleConsumerConfigurationStore(IReadOnlyDictionary<string, IConsumerConfigurationStore> stores,
    string environment) : IConsumerConfigurationStore, IDisposable
{
    public static string Repository(string module) => module switch
    {
        "library" => "https://github.com/aetheric-forge/library",
        "archive" => "https://github.com/aetheric-forge/archive",
        "decisions" => "https://github.com/aetheric-forge/adr-campus",
        _ => throw new ConsumerProvisioningException("consumer.unknown_module")
    };

    public Task WriteAsync(ConsumerConfiguration configuration, CancellationToken ct)
    {
        if (configuration.Module != configuration.InstitutionId || configuration.Environment != environment
            || configuration.DeploymentId != InstitutionDeploymentAttempt.Identity(Repository(configuration.Module), configuration.Module, environment))
            throw new ConsumerProvisioningException("consumer.target_identity_mismatch");
        if (!stores.TryGetValue(configuration.Module, out var store))
            throw new ConsumerProvisioningException("consumer.target_unconfigured");
        return store.WriteAsync(configuration, ct);
    }

    public void Dispose()
    { foreach (var store in stores.Values.Distinct()) (store as IDisposable)?.Dispose(); }
}
