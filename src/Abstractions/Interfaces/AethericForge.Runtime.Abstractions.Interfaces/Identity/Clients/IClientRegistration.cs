namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Clients;

public interface IClientRegistration
{
    string ClientId { get; }
    string? DisplayName { get; }
    bool Enabled { get; }
    bool PublicClient { get; }
    bool StandardFlowEnabled { get; }
    bool DirectAccessGrantsEnabled { get; }
    IReadOnlyCollection<string> RedirectUris { get; }
    IReadOnlyCollection<string> WebOrigins { get; }

    /// <summary>
    /// The client secret. Populated only immediately after
    /// <see cref="AethericForge.Runtime.Abstractions.Interfaces.Identity.Services.IRegistryClerk.RegisterClientAsync"/>
    /// or a future secret-rotation call - never returned by a read/list operation, and always
    /// <see langword="null"/> for a public client.
    /// </summary>
    string? Secret { get; }
}
