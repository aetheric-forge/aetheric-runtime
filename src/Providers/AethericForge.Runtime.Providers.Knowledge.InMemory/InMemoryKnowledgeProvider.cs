using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.References;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Knowledge.Artifacts;
using AethericForge.Runtime.Models.Knowledge.Primitives;

namespace AethericForge.Runtime.Providers.Knowledge.InMemory;

public sealed class InMemoryKnowledgeProvider : IKnowledgeProvider
{
    private readonly object _sync = new();
    private readonly Dictionary<string, IKnowledgeArtifact> _artifacts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IKnowledgeReference> _authoritativeReferences = new(StringComparer.Ordinal);

    public InMemoryKnowledgeProvider(string scheme)
    {
        Scheme = NormalizeRequired(scheme, nameof(scheme));
    }

    public string Scheme { get; }

    public Task<IKnowledgeArtifact?> GetArtifactAsync(
        IKnowledgeReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        var key = GetKey(reference);
        lock (_sync)
        {
            return Task.FromResult(_artifacts.TryGetValue(key, out var artifact) ? artifact : null);
        }
    }

    public Task<IKnowledgeArtifact> StoreArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        IKnowledgeAuthority? authority = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(representations);
        cancellationToken.ThrowIfCancellationRequested();

        // In-memory provider creates a reference based on the scheme and a new GUID for Name
        var reference = new KnowledgeReference(
            set: Scheme,
            kind: "Artifact",
            name: Guid.NewGuid().ToString("N"),
            version: "1.0.0");

        var artifact = new KnowledgeArtifact(
            reference,
            descriptor,
            representations,
            lineage,
            authority: authority);

        var key = GetKey(reference);
        lock (_sync)
        {
            _artifacts[key] = artifact;
        }

        return Task.FromResult<IKnowledgeArtifact>(artifact);
    }

    public Task SetAuthoritativeReferenceAsync(
        IAuthoritativeReference reference,
        IKnowledgeReference target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        var key = GetKey(reference);
        lock (_sync)
        {
            _authoritativeReferences[key] = target;
        }

        return Task.CompletedTask;
    }

    public Task<IKnowledgeReference?> ResolveAuthoritativeReferenceAsync(
        IAuthoritativeReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        var key = GetKey(reference);
        lock (_sync)
        {
            return Task.FromResult(_authoritativeReferences.TryGetValue(key, out var target) ? target : null);
        }
    }

    private static string GetKey(IKnowledgeReference reference)
    {
        return $"{reference.Set}:{reference.Kind}/{reference.Name}@{reference.Version}.{reference.Revision}";
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
