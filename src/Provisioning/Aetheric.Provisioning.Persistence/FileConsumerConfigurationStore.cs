using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.Persistence;

/// <summary>Exports a single consumer artifact per institution into a private deployment directory.</summary>
public sealed class FileConsumerConfigurationStore(string directory) : IConsumerConfigurationStore
{
    public Task WriteAsync(ConsumerConfiguration configuration, CancellationToken ct)
    {
        if (configuration.Version != 1 || !Regex.IsMatch(configuration.DeploymentId, "\\A[0-9a-f]{64}\\z"))
            throw new ArgumentException("Invalid consumer configuration identity.");
        var folder = Path.Combine(Path.GetFullPath(directory), configuration.DeploymentId);
        // Establish private permissions before materializing secret values.
        _ = new FileStorage(folder);
        return FileStorage.WriteAsync(Path.Combine(folder, "configuration.json"), JsonSerializer.SerializeToUtf8Bytes(configuration), ct);
    }
}
