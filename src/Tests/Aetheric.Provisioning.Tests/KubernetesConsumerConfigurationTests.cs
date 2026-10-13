using System.Collections.Immutable;
using System.Net;
using System.Text.Json;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Worker;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class KubernetesConsumerConfigurationTests
{
    [Fact]
    public async Task Consumer_secret_update_is_atomic_and_carries_only_the_selected_configuration()
    {
        var handler = new SecretHandler("""{"metadata":{"name":"parallel-you-consumer","resourceVersion":"17"},"data":{}}""");
        using var store = Store(handler);
        await store.WriteAsync(Manifest(), default);
        var patch = handler.Patch!.Value;
        Assert.Equal("17", patch.GetProperty("metadata").GetProperty("resourceVersion").GetString());
        Assert.Equal(new string('a', 64), patch.GetProperty("metadata").GetProperty("annotations").GetProperty("aetheric-forge/deployment-id").GetString());
        var settings = JsonSerializer.Deserialize<ConsumerConfiguration>(patch.GetProperty("stringData").GetProperty("configuration.json").GetString()!)!;
        Assert.Equal("application-secret", settings.Settings["ParallelYou:MongoDb:Password"]);
        Assert.Equal("application/merge-patch+json", handler.ContentType);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("""{"metadata":{"name":"parallel-you-consumer","resourceVersion":"17","annotations":{"aetheric-forge/deployment-id":"different-deployment"}}} """)]
    [InlineData("""{"metadata":{"name":"parallel-you-consumer","resourceVersion":"17"},"data":{"existing-secret":"private"}} """)]
    public async Task A_secret_owned_by_another_deployment_or_containing_unclaimed_data_is_preserved(string existing)
    {
        var handler = new SecretHandler(existing);
        using var store = Store(handler);
        var failure = await Assert.ThrowsAsync<ConsumerProvisioningException>(() => store.WriteAsync(Manifest(), default));
        Assert.Equal("consumer.secret_owner_conflict", failure.Code);
        Assert.Null(handler.Patch);
    }

    [Fact]
    public async Task Failed_update_is_not_reported_as_successful_delivery()
    {
        var handler = new SecretHandler("""{"metadata":{"name":"parallel-you-consumer","resourceVersion":"17"},"data":{}}""") { PatchStatus = HttpStatusCode.Conflict };
        using var store = Store(handler);
        var failure = await Assert.ThrowsAsync<ConsumerProvisioningException>(() => store.WriteAsync(Manifest(), default));
        Assert.Equal("consumer.secret_update_failed", failure.Code);
    }

    private static KubernetesConsumerConfigurationStore Store(SecretHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://kubernetes.example/") }, "aetheric-forge", "parallel-you-consumer");
    private static ConsumerConfiguration Manifest() => new(1, "parallel-you", new string('a', 64), "parallel-you", "1.0.0", "test",
        new string('b', 64), new Dictionary<string, string> { ["ParallelYou:MongoDb:Password"] = "application-secret" }.ToImmutableDictionary());

    private sealed class SecretHandler(string existing) : HttpMessageHandler
    {
        public JsonElement? Patch { get; private set; }
        public string? ContentType { get; private set; }
        public int Calls { get; private set; }
        public HttpStatusCode PatchStatus { get; init; } = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal("/api/v1/namespaces/aetheric-forge/secrets/parallel-you-consumer", request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Get) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(existing) };
            Assert.Equal(HttpMethod.Patch, request.Method);
            ContentType = request.Content!.Headers.ContentType!.MediaType;
            using var json = JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct));
            Patch = json.RootElement.Clone();
            return new HttpResponseMessage(PatchStatus) { Content = new StringContent("{}") };
        }
    }
}
