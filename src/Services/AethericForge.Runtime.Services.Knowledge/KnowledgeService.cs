using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.References;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;

namespace AethericForge.Runtime.Services.Knowledge;

public sealed class KnowledgeService : IKnowledgeService
{
    private readonly IReadOnlyDictionary<string, IKnowledgeProvider> _providers;

    public KnowledgeService(IEnumerable<IKnowledgeProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToDictionary(p => p.Scheme, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IKnowledgeArtifact?> GetArtifactAsync(IKnowledgeReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (_providers.TryGetValue(reference.Set, out var provider))
        {
            return await provider.GetArtifactAsync(reference, cancellationToken);
        }

        return null;
    }

    public async Task<IKnowledgeArtifact> PublishArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        CancellationToken cancellationToken = default)
    {
        // For now, we'll default to a primary provider if available, or throw.
        var provider = _providers.Values.FirstOrDefault() 
            ?? throw new InvalidOperationException("No knowledge providers available.");

        return await provider.StoreArtifactAsync(descriptor, representations, lineage, cancellationToken);
    }

    public async Task<IKnowledgeArtifact?> ResolveReferenceAsync(
        IKnowledgeReference reference,
        CancellationToken cancellationToken = default)
    {
        if (reference is IAuthoritativeReference authRef)
        {
            // TODO: In a real system, we would query authoritative claims to find the fixed reference.
            // For now, we'll just resolve it as a normal reference.
        }

        return await GetArtifactAsync(reference, cancellationToken);
    }
}
