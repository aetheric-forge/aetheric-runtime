using Amazon;
using Amazon.S3;

namespace AethericForge.Runtime.IntegrationTests;

internal static class EnvironmentConfiguration
{
    public static string Require(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Required integration-test environment variable '{name}' is not set.");

    public static string Get(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    public static AmazonS3Client CreateS3Client()
    {
        var config = new AmazonS3Config();
        var serviceUrl = Environment.GetEnvironmentVariable("AF_E2E_S3_SERVICE_URL");

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            config.ServiceURL = serviceUrl;
            config.ForcePathStyle = Get("AF_E2E_S3_FORCE_PATH_STYLE", "false")
                .Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(
                Get("AF_E2E_S3_REGION", Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1"));
        }

        return new AmazonS3Client(config);
    }
}
