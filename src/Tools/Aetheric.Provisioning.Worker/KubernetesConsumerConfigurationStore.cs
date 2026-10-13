using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.Worker;

/// <summary>Updates one pre-created consumer Secret using the worker's service account.</summary>
public sealed class KubernetesConsumerConfigurationStore(HttpClient http, string namespaceName, string secretName,
    X509Certificate2? clusterRoot = null) : IConsumerConfigurationStore, IDisposable
{
    public static async Task<KubernetesConsumerConfigurationStore> FromServiceAccountAsync(string namespaceName, string secretName)
    {
        var token = (await File.ReadAllTextAsync("/var/run/secrets/kubernetes.io/serviceaccount/token")).Trim();
        if (token.Length == 0) throw new InvalidDataException("The worker service account token is missing.");
        var root = X509Certificate2.CreateFromPemFile("/var/run/secrets/kubernetes.io/serviceaccount/ca.crt");
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) =>
            {
                if (certificate is null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
                    return false;
                using var trusted = new X509Chain();
                trusted.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                trusted.ChainPolicy.CustomTrustStore.Add(root);
                trusted.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                trusted.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
                if (chain is not null)
                    foreach (var element in chain.ChainElements.Cast<X509ChainElement>().Skip(1))
                        trusted.ChainPolicy.ExtraStore.Add(element.Certificate);
                return trusted.Build(certificate);
            }
        };
        var http = new HttpClient(new ServiceAccountTokenHandler(handler))
        { BaseAddress = new Uri("https://kubernetes.default.svc/"), Timeout = TimeSpan.FromSeconds(30) };
        return new KubernetesConsumerConfigurationStore(http, namespaceName, secretName, root);
    }

    public async Task WriteAsync(ConsumerConfiguration configuration, CancellationToken ct)
    {
        if (!Regex.IsMatch(namespaceName, "\\A[a-z0-9]([-a-z0-9]{0,61}[a-z0-9])?\\z")
            || !Regex.IsMatch(secretName, "\\A[a-z0-9]([-a-z0-9.]{0,251}[a-z0-9])?\\z"))
            throw new ArgumentException("A valid consumer Secret namespace and name are required.");
        var path = $"api/v1/namespaces/{namespaceName}/secrets/{secretName}";
        using var existing = await http.GetAsync(path, ct);
        if (!existing.IsSuccessStatusCode) throw new ConsumerProvisioningException("consumer.secret_target_unavailable");
        using var document = JsonDocument.Parse(await existing.Content.ReadAsStringAsync(ct));
        var metadata = document.RootElement.GetProperty("metadata");
        if (metadata.GetProperty("name").GetString() != secretName)
            throw new ConsumerProvisioningException("consumer.secret_identity_mismatch");
        string? owner = null;
        if (metadata.TryGetProperty("annotations", out var annotations)
            && annotations.TryGetProperty("aetheric-forge/deployment-id", out var ownerNode)) owner = ownerNode.GetString();
        if (owner is not null && owner != configuration.DeploymentId
            || owner is null && document.RootElement.TryGetProperty("data", out var data) && data.EnumerateObject().Any())
            throw new ConsumerProvisioningException("consumer.secret_owner_conflict");
        var revision = metadata.GetProperty("resourceVersion").GetString();
        if (string.IsNullOrWhiteSpace(revision)) throw new ConsumerProvisioningException("consumer.secret_revision_missing");
        var patch = new
        {
            metadata = new
            {
                resourceVersion = revision,
                annotations = new Dictionary<string, string>
                {
                    ["aetheric-forge/deployment-id"] = configuration.DeploymentId,
                    ["aetheric-forge/plan-id"] = configuration.PlanId
                }
            },
            stringData = new Dictionary<string, string> { ["configuration.json"] = JsonSerializer.Serialize(configuration) }
        };
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        { Content = new StringContent(JsonSerializer.Serialize(patch), System.Text.Encoding.UTF8, "application/merge-patch+json") };
        using var result = await http.SendAsync(request, ct);
        if (!result.IsSuccessStatusCode) throw new ConsumerProvisioningException("consumer.secret_update_failed");
    }

    public void Dispose() { http.Dispose(); clusterRoot?.Dispose(); }

    private sealed class ServiceAccountTokenHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            // Projected service account tokens rotate while the broker worker remains running.
            var token = (await File.ReadAllTextAsync("/var/run/secrets/kubernetes.io/serviceaccount/token", ct)).Trim();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await base.SendAsync(request, ct);
        }
    }
}
