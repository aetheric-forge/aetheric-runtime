using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.References;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;

namespace AethericForge.Runtime.Services.Knowledge;

public sealed class KnowledgeService : IKnowledgeService
{
    private readonly IReadOnlyDictionary<string, IKnowledgeProvider> _providers;

    public KnowledgeService(IEnumerable<IKnowledgeProvider> providers, ITeam<ICuratorClerk> team)
    {
        Team = team;
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToDictionary(p => p.Scheme, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IKnowledgeArtifact?> GetArtifactAsync(IKnowledgeReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (_providers.TryGetValue(reference.Scheme, out var provider))
        {
            return await provider.GetArtifactAsync(reference, cancellationToken);
        }

        return null;
    }

    public async Task<IReadOnlyCollection<IKnowledgeArtifact>> FindArtifactsAsync(
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authority);

        var searches = _providers.Values
            .Select(provider => provider.FindArtifactsAsync(authority, cancellationToken));
        var results = await Task.WhenAll(searches);

        return results
            .SelectMany(artifacts => artifacts)
            .OrderByDescending(artifact => artifact.CreatedAtUtc)
            .ToArray();
    }

    public async Task<IKnowledgeArtifact> PublishArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        IKnowledgeAuthority? authority = null,
        CancellationToken cancellationToken = default)
    {
        // For now, we'll default to a primary provider if available, or throw.
        var provider = _providers.Values.FirstOrDefault() 
            ?? throw new InvalidOperationException("No knowledge providers available.");

        return await provider.StoreArtifactAsync(descriptor, representations, lineage, authority, cancellationToken);
    }

    public async Task<IKnowledgeArtifact?> ResolveReferenceAsync(
        IKnowledgeReference reference,
        CancellationToken cancellationToken = default)
    {
        if (reference is IAuthoritativeReference authRef)
        {
            if (_providers.TryGetValue(authRef.Scheme, out var provider))
            {
                var resolvedReference = await provider.ResolveAuthoritativeReferenceAsync(authRef, cancellationToken);
                if (resolvedReference != null)
                {
                    return await GetArtifactAsync(resolvedReference, cancellationToken);
                }
            }
        }

        return await GetArtifactAsync(reference, cancellationToken);
    }

    public async Task SetAuthoritativeReferenceAsync(
        IAuthoritativeReference reference, 
        IKnowledgeReference target, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(target);

        if (_providers.TryGetValue(reference.Scheme, out var provider))
        {
            await provider.SetAuthoritativeReferenceAsync(reference, target, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException($"No provider found for scheme '{reference.Scheme}'.");
        }
    }

    public ITeam<ICuratorClerk> Team { get; }
}
