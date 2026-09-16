namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Clients;

/// <summary>
/// Input to
/// <see cref="AethericForge.Runtime.Abstractions.Interfaces.Identity.Services.IRegistryClerk.RegisterClientAsync"/>
/// and <see cref="AethericForge.Runtime.Abstractions.Interfaces.Identity.Services.IRegistryClerk.UpdateClientAsync"/>.
/// </summary>
public sealed record ClientRegistrationRequest(
    string ClientId,
    string? DisplayName = null,
    bool PublicClient = false,
    bool StandardFlowEnabled = true,
    bool DirectAccessGrantsEnabled = false,
    IReadOnlyCollection<string>? RedirectUris = null,
    IReadOnlyCollection<string>? WebOrigins = null);
