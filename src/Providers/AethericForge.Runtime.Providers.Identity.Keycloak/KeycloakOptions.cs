namespace AethericForge.Runtime.Providers.Identity.Keycloak;

public sealed class KeycloakOptions
{
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Realm { get; set; } = string.Empty;
    public string? AdminApiBaseAddress { get; set; }
    public TimeSpan DirectoryFreshnessLifetime { get; set; } = TimeSpan.FromMinutes(1);
}
