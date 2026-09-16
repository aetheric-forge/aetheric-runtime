using AethericForge.Runtime.Abstractions.Interfaces.Identity.Clients;
using AethericForge.Runtime.Models.Identity.Directory;

namespace AethericForge.Runtime.Models.Identity.Clients;

public sealed record ClientRegistration : IClientRegistration
{
    public ClientRegistration(
        string clientId,
        string? displayName,
        bool enabled,
        bool publicClient,
        bool standardFlowEnabled,
        bool directAccessGrantsEnabled,
        IReadOnlyCollection<string>? redirectUris = null,
        IReadOnlyCollection<string>? webOrigins = null,
        string? secret = null)
    {
        ClientId = DirectoryValue.NormalizeRequired(clientId, nameof(clientId));
        DisplayName = DirectoryValue.NormalizeOptional(displayName);
        Enabled = enabled;
        PublicClient = publicClient;
        StandardFlowEnabled = standardFlowEnabled;
        DirectAccessGrantsEnabled = directAccessGrantsEnabled;
        RedirectUris = redirectUris ?? [];
        WebOrigins = webOrigins ?? [];

        if (publicClient && secret is not null)
        {
            throw new ArgumentException("A public client cannot have a secret.", nameof(secret));
        }

        Secret = DirectoryValue.NormalizeOptional(secret);
    }

    public string ClientId { get; }
    public string? DisplayName { get; }
    public bool Enabled { get; }
    public bool PublicClient { get; }
    public bool StandardFlowEnabled { get; }
    public bool DirectAccessGrantsEnabled { get; }
    public IReadOnlyCollection<string> RedirectUris { get; }
    public IReadOnlyCollection<string> WebOrigins { get; }
    public string? Secret { get; }
}
