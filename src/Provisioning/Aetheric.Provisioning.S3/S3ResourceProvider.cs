using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aetheric.Provisioning.Engine;
using Amazon.S3;
using Amazon.S3.Model;

namespace Aetheric.Provisioning.S3;

/// <summary>
/// Ensures an S3/MinIO bucket exists and carries a bucket policy denying insecure (non-TLS)
/// transport. Unlike RabbitMq/MongoDb/Keycloak, this provider does not mint a per-institution
/// scoped credential - v1 reuses the same root access key for every institution's Archive
/// resource (decided tradeoff: a second MinIO-Admin-API integration for per-institution scoped
/// keys is significantly more surface than one bucket-policy call, and isn't justified until an
/// actual deployment need shows up - see docs/roadmap.md's "demand-driven expansion"). The bucket
/// itself is this resource's only real isolation boundary for now.
/// </summary>
public sealed class S3ResourceProvider : IResourceProvider, IDisposable
{
    private static readonly Regex BucketPattern = new(@"\A[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]\z", RegexOptions.Compiled);

    private readonly IAmazonS3 _client;

    public S3ResourceProvider(RootCredential rootCredential)
    {
        ArgumentNullException.ThrowIfNull(rootCredential);
        var username = rootCredential.Username ?? throw new ArgumentException("Root credential requires a username.", nameof(rootCredential));
        var options = rootCredential.S3 ?? new S3RootOptions();
        var config = new AmazonS3Config
        {
            ServiceURL = new UriBuilder(options.Scheme, rootCredential.Host, rootCredential.Port).Uri.ToString(),
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region,
        };
        _client = new AmazonS3Client(username, rootCredential.Password, config);
    }

    public string Key => "s3";

    public ImmutableArray<ValidationIssue> Validate(ResourceRequirement resource, ResourceBinding binding)
    {
        var valid = resource.Ownership == "owned" && binding.Provider == Key
            && binding.Settings.TryGetValue("bucket", out var bucket) && BucketPattern.IsMatch(bucket)
            && binding.Settings.Keys.All(k => k is "bucket")
            && binding.Secrets.IsEmpty;
        return valid ? [] : [new("s3.binding", resource.Id,
            "S3 requires an owned resource with only a bucket setting (3-63 characters: lowercase letters, digits, '.', '-'; must start and end with a letter or digit). Credentials are supplied by the host.")];
    }

    public async Task<ProviderResult> EnsureAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        if (!Validate(context.Resource, context.Binding).IsEmpty)
            throw new InvalidOperationException("Invalid S3 binding.");

        var bucket = context.Binding.Settings["bucket"];
        var alreadyExists = await BucketExistsAsync(bucket, cancellationToken);
        if (!alreadyExists) await _client.PutBucketAsync(new PutBucketRequest { BucketName = bucket });

        // Idempotent regardless of AlreadyExists - a prior run may have created the bucket but
        // failed before the policy step, and PutBucketPolicy is safe to repeat either way.
        await _client.PutBucketPolicyAsync(new PutBucketPolicyRequest { BucketName = bucket, Policy = DenyInsecureTransportPolicy(bucket) });

        return new(alreadyExists, ImmutableArray<SecretReference>.Empty);
    }

    private async Task<bool> BucketExistsAsync(string bucket, CancellationToken ct)
    {
        var buckets = await _client.ListBucketsAsync(new ListBucketsRequest(), ct);
        // Buckets is null (not an empty list) when the account has zero buckets at all - an
        // AWSSDK/MinIO XML-deserialization quirk (an empty <Buckets/> element yields no list),
        // not something safe to assume away.
        return (buckets.Buckets ?? []).Any(b => string.Equals(b.BucketName, bucket, StringComparison.Ordinal));
    }

    private static string DenyInsecureTransportPolicy(string bucket) => $$"""
        {
          "Version": "2012-10-17",
          "Statement": [
            {
              "Sid": "DenyInsecureTransport",
              "Effect": "Deny",
              "Principal": "*",
              "Action": "s3:*",
              "Resource": ["arn:aws:s3:::{{bucket}}", "arn:aws:s3:::{{bucket}}/*"],
              "Condition": { "Bool": { "aws:SecureTransport": "false" } }
            }
          ]
        }
        """;

    public void Dispose() => _client.Dispose();
}
