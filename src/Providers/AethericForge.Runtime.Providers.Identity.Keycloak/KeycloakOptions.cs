namespace AethericForge.Runtime.Providers.Identity.Keycloak;

public sealed class KeycloakOptions
{
    /// <summary>
    /// The Keycloak server's base URL, e.g. <c>https://sso.example.com</c> — no <c>/realms/...</c>
    /// suffix. The realm-scoped authority and admin API base are both derived from this and
    /// <see cref="Realm"/>.
    /// </summary>
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// Advanced override for deployments where the Admin REST API is not reachable at
    /// <c>{Authority}/admin/</c> (e.g. a split ingress). Leave unset in the common case.
    /// </summary>
    public string? AdminApiBaseAddress { get; set; }
    public TimeSpan DirectoryFreshnessLifetime { get; set; } = TimeSpan.FromMinutes(1);
}
