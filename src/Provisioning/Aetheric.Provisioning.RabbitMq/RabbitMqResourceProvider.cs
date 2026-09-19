using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.RabbitMq;

/// <summary>
/// Ensures a RabbitMQ vhost exists and grants a resource-scoped user full permissions on it, via
/// the management HTTP API. The host supplies root credentials for the target broker (the same
/// ones already used for connectivity testing during bootstrap) - connections never enter a plan.
/// </summary>
public sealed class RabbitMqResourceProvider : IResourceProvider, IDisposable
{
    private static readonly Regex VhostPattern = new(@"\A[a-zA-Z0-9._-]{1,255}\z", RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly string _username;
    private readonly string _password;

    public RabbitMqResourceProvider(RootCredential rootCredential, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(rootCredential);
        _username = rootCredential.Username ?? throw new ArgumentException("Root credential requires a username.", nameof(rootCredential));
        _password = rootCredential.Password;
        var options = rootCredential.RabbitMq ?? new RabbitMqRootOptions();
        var origin = new UriBuilder(options.Scheme, rootCredential.Host, rootCredential.Port, options.BasePath).Uri;
        // handler is a test seam - production callers never pass one, so this always owns and
        // disposes a real HttpClientHandler with auto-redirect off.
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = origin };
    }

    public string Key => "rabbitmq";

    public ImmutableArray<ValidationIssue> Validate(ResourceRequirement resource, ResourceBinding binding)
    {
        var valid = resource.Ownership == "owned" && binding.Provider == Key
            && binding.Settings.TryGetValue("vhost", out var vhost) && VhostPattern.IsMatch(vhost)
            && binding.Settings.Keys.All(k => k is "vhost")
            && binding.Secrets.IsEmpty;
        return valid ? [] : [new("rabbitmq.binding", resource.Id,
            "RabbitMQ requires an owned resource with only a vhost setting (1-255 characters: letters, digits, '.', '_', '-'). Credentials are supplied by the host.")];
    }

    public async Task<ProviderResult> EnsureAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        if (!Validate(context.Resource, context.Binding).IsEmpty)
            throw new InvalidOperationException("Invalid RabbitMQ binding.");

        var vhost = context.Binding.Settings["vhost"];
        var alreadyExists = await VhostExistsAsync(vhost, cancellationToken);
        if (!alreadyExists) await PutAsync($"api/vhosts/{Uri.EscapeDataString(vhost)}", new { }, cancellationToken);

        var secret = await context.Secrets.GetOrCreateAsync("rabbitmq", $"{vhost}-{context.Resource.Id}", cancellationToken);
        var password = await context.Secrets.ReadAsync(secret, cancellationToken);
        var scopedUser = $"{context.InstitutionId}-{context.Resource.Id}";

        // Idempotent regardless of AlreadyExists - a prior run may have created the vhost but
        // failed before the user/permissions step, and PUT is safe to repeat either way.
        await PutAsync($"api/users/{Uri.EscapeDataString(scopedUser)}", new { password, tags = "" }, cancellationToken);
        await PutAsync($"api/permissions/{Uri.EscapeDataString(vhost)}/{Uri.EscapeDataString(scopedUser)}",
            new { configure = ".*", write = ".*", read = ".*" }, cancellationToken);

        return new(alreadyExists, [secret]);
    }

    private async Task<bool> VhostExistsAsync(string vhost, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/vhosts/{Uri.EscapeDataString(vhost)}");
        request.Headers.Authorization = AuthorizationHeader();
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    private async Task PutAsync(string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = AuthorizationHeader();
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private AuthenticationHeaderValue AuthorizationHeader() =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}")));

    public void Dispose() => _http.Dispose();
}
