using AethericForge.Runtime.Providers.Identity.Keycloak;

namespace Aetheric.Provisioning.Registry;

// The optional Admin API origin is deployment configuration, never browser input.
// Token acquisition and issuer verification continue to use the public Authority.
internal static class BootstrapKeycloakEndpoints
{
    public static Uri AdminRealm(KeycloakOptions options)
    {
        var address = string.IsNullOrWhiteSpace(options.AdminApiBaseAddress)
            ? options.Authority.TrimEnd('/') + "/admin/" : options.AdminApiBaseAddress;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != "https" || endpoint.UserInfo.Length != 0
            || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0
            || (!string.IsNullOrWhiteSpace(options.AdminApiBaseAddress)
                && endpoint.AbsolutePath.TrimEnd('/') != "/admin"))
            throw new ArgumentException("Invalid bootstrap Keycloak Admin API configuration.");
        return new Uri(new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/"),
            "realms/" + Uri.EscapeDataString(options.Realm) + "/");
    }
}
