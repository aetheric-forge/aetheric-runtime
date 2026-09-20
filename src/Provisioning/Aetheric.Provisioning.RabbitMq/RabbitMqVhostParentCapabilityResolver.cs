using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.RabbitMq;

/// <summary>
/// Live-verifies a Post Office parent dependency ("IPostOffice") - the RabbitMQ counterpart to
/// Aetheric.Provisioning.Keycloak's KeycloakRealmParentCapabilityResolver. Deliberately narrow -
/// recognizes exactly one contract, returns false for anything else - so composing it with the
/// other systems' resolvers (CompositeParentCapabilityResolver) is a plain OR: only the one whose
/// contract matches ever performs real I/O.
/// </summary>
public sealed class RabbitMqVhostParentCapabilityResolver : IParentCapabilityResolver, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _username;
    private readonly string _password;
    private readonly string _vhost;

    public RabbitMqVhostParentCapabilityResolver(RootCredential rootCredential, string vhost, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(rootCredential);
        if (string.IsNullOrWhiteSpace(vhost)) throw new ArgumentException("A vhost is required.", nameof(vhost));
        _username = rootCredential.Username ?? throw new ArgumentException("Root credential requires a username.", nameof(rootCredential));
        _password = rootCredential.Password;
        var options = rootCredential.RabbitMq ?? new RabbitMqRootOptions();
        var origin = new UriBuilder(options.Scheme, rootCredential.Host, rootCredential.Port, options.BasePath).Uri;
        _vhost = vhost;
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = origin };
    }

    public async Task<bool> IsAvailableAsync(ParentContext parent, string contract, string source, CancellationToken cancellationToken)
    {
        if (contract != "IPostOffice") return false;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/vhosts/{Uri.EscapeDataString(_vhost)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}")));
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    public void Dispose() => _http.Dispose();
}
