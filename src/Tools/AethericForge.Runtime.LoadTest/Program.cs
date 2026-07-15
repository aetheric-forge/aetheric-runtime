using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Models.Archive.Primitives;
using AethericForge.Runtime.Models.Post;
using AethericForge.Runtime.Providers.Archive.MongoDb;
using AethericForge.Runtime.Providers.Archive.S3;
using AethericForge.Runtime.Providers.Post.RabbitMq;
using Amazon;
using Amazon.S3;

var options = LoadOptions.FromEnvironment();
Console.WriteLine($"Aetheric Runtime load test: target={options.Target}, concurrency={options.Concurrency}, duration={options.Duration}, payload={options.PayloadBytes} bytes");

var runId = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
var latencies = new ConcurrentBag<double>();
var errors = new ConcurrentBag<string>();
long completed = 0;
using var duration = new CancellationTokenSource(options.Duration);

var workers = Enumerable.Range(0, options.Concurrency)
    .Select(worker => RunWorkerAsync(worker, duration.Token))
    .ToArray();

await Task.WhenAll(workers);

var samples = latencies.Order().ToArray();
var elapsedSeconds = options.Duration.TotalSeconds;
var errorRate = completed + errors.Count == 0 ? 0 : (double)errors.Count / (completed + errors.Count);
Console.WriteLine($"completed={completed} errors={errors.Count} error_rate={errorRate:P2} throughput={completed / elapsedSeconds:F2} ops/s");
if (samples.Length > 0)
{
    Console.WriteLine($"latency_ms p50={Percentile(samples, .50):F2} p95={Percentile(samples, .95):F2} p99={Percentile(samples, .99):F2} max={samples[^1]:F2}");
}

foreach (var group in errors.GroupBy(x => x).OrderByDescending(x => x.Count()).Take(5))
    Console.WriteLine($"error[{group.Count()}]: {group.Key}");

return errorRate <= options.MaxErrorRate &&
       (samples.Length == 0 || Percentile(samples, .95) <= options.MaxP95Milliseconds)
    ? 0
    : 1;

async Task RunWorkerAsync(int worker, CancellationToken ct)
{
    await using var operation = CreateOperation(worker, runId, options);
    var sequence = 0L;

    while (!ct.IsCancellationRequested)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await operation.ExecuteAsync(sequence++, ct);
            stopwatch.Stop();
            latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
            Interlocked.Increment(ref completed);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex)
        {
            errors.Add($"{ex.GetType().Name}: {ex.Message}");
        }
    }
}

static ILoadOperation CreateOperation(int worker, string runId, LoadOptions options) =>
    options.Target switch
    {
        "mongodb" => new ArchiveLoadOperation(
            new MongoDbArchiveProvider(
                Require("AF_E2E_MONGODB_URI"),
                Get("AF_E2E_MONGODB_DATABASE", "aetheric_runtime_e2e"),
                Get("AF_E2E_MONGODB_COLLECTION", "archive_load"),
                "load-mongodb",
                bool.Parse(Get("AF_E2E_MONGODB_DIRECT_CONNECTION", "false"))),
            worker, runId, options.PayloadBytes),
        "s3" => CreateS3Operation(worker, runId, options.PayloadBytes),
        "rabbitmq" => new RabbitMqLoadOperation(
            new RabbitMqPostProvider(
                Get("AF_E2E_RABBITMQ_DOMAIN", "load"),
                Require("AF_E2E_RABBITMQ_URI")),
            worker, runId),
        _ => throw new InvalidOperationException("AF_LOAD_TARGET must be mongodb, s3, or rabbitmq.")
    };

static ArchiveLoadOperation CreateS3Operation(int worker, string runId, int payloadBytes)
{
    var config = new AmazonS3Config();
    var serviceUrl = Environment.GetEnvironmentVariable("AF_E2E_S3_SERVICE_URL");
    if (!string.IsNullOrWhiteSpace(serviceUrl))
    {
        config.ServiceURL = serviceUrl;
        config.ForcePathStyle = bool.Parse(Get("AF_E2E_S3_FORCE_PATH_STYLE", "false"));
    }
    else
    {
        config.RegionEndpoint = RegionEndpoint.GetBySystemName(
            Get("AF_E2E_S3_REGION", Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1"));
    }

    var client = new AmazonS3Client(config);
    var provider = new S3ArchiveProvider(
        client,
        "load-s3",
        Require("AF_E2E_S3_BUCKET"),
        Get("AF_E2E_S3_KEY_PREFIX", "aetheric-runtime-load"));
    return new ArchiveLoadOperation(provider, worker, runId, payloadBytes, client);
}

static string Require(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
    ? value
    : throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
static string Get(string name, string fallback) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
static double Percentile(double[] sorted, double percentile) => sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)];

internal interface ILoadOperation : IAsyncDisposable
{
    Task ExecuteAsync(long sequence, CancellationToken ct);
}

internal sealed class ArchiveLoadOperation(
    IArchiveProvider provider,
    int worker,
    string runId,
    int payloadBytes,
    IDisposable? ownedResource = null) : ILoadOperation
{
    private readonly byte[] _payload = CreatePayload(payloadBytes);

    public async Task ExecuteAsync(long sequence, CancellationToken ct)
    {
        var key = $"runs/{runId}/workers/{worker}/{sequence}";
        var reference = await provider.PutAsync(
            key,
            new MemoryStream(_payload, writable: false),
            new ArchiveMetadata("application/octet-stream", _payload.Length),
            ct);
        try
        {
            await using var content = await provider.RetrieveAsync(reference, ct);
            using var sink = new MemoryStream();
            await content.CopyToAsync(sink, ct);
            if (!sink.ToArray().AsSpan().SequenceEqual(_payload))
                throw new InvalidDataException("Retrieved archive payload did not match.");
        }
        finally
        {
            await provider.DeleteAsync(reference, CancellationToken.None);
        }
    }

    public ValueTask DisposeAsync()
    {
        ownedResource?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static byte[] CreatePayload(int size)
    {
        var seed = Encoding.UTF8.GetBytes("aetheric-forge-runtime-load-test-");
        return Enumerable.Range(0, size).Select(i => seed[i % seed.Length]).ToArray();
    }
}

internal sealed class RabbitMqLoadOperation(
    RabbitMqPostProvider provider,
    int worker,
    string runId) : ILoadOperation
{
    public Task ExecuteAsync(long sequence, CancellationToken ct)
    {
        var domain = provider.Name;
        var reference = new PostReference(
            domain,
            $"load.{runId}.{worker}",
            new PostContract("publish", "1", PostIntent.Event));
        return provider.PublishAsync(new PostEnvelope<object>(
            reference,
            new { RunId = runId, Worker = worker, Sequence = sequence },
            new PostMetadata(correlationId: runId)), ct);
    }

    public ValueTask DisposeAsync() => provider.DisposeAsync();
}

internal sealed record LoadOptions(
    string Target,
    int Concurrency,
    TimeSpan Duration,
    int PayloadBytes,
    double MaxErrorRate,
    double MaxP95Milliseconds)
{
    public static LoadOptions FromEnvironment() => new(
        Target: GetValue("AF_LOAD_TARGET", "mongodb").ToLowerInvariant(),
        Concurrency: int.Parse(GetValue("AF_LOAD_CONCURRENCY", "4")),
        Duration: TimeSpan.FromSeconds(int.Parse(GetValue("AF_LOAD_DURATION_SECONDS", "30"))),
        PayloadBytes: int.Parse(GetValue("AF_LOAD_PAYLOAD_BYTES", "1024")),
        MaxErrorRate: double.Parse(GetValue("AF_LOAD_MAX_ERROR_RATE", "0.01"), System.Globalization.CultureInfo.InvariantCulture),
        MaxP95Milliseconds: double.Parse(GetValue("AF_LOAD_MAX_P95_MS", "1000"), System.Globalization.CultureInfo.InvariantCulture));

    private static string GetValue(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
}
