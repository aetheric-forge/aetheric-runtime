using Aetheric.Provisioning.Engine;
using Amazon.S3;
using Amazon.S3.Model;

namespace Aetheric.Provisioning.S3;

/// <summary>
/// Live-verifies an Archive parent dependency ("IArchive") - the S3/MinIO counterpart to
/// Aetheric.Provisioning.Keycloak's KeycloakRealmParentCapabilityResolver. Deliberately narrow -
/// recognizes exactly one contract, returns false for anything else - so composing it with the
/// other systems' resolvers (CompositeParentCapabilityResolver) is a plain OR: only the one whose
/// contract matches ever performs real I/O.
/// </summary>
public sealed class S3BucketParentCapabilityResolver : IParentCapabilityResolver, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3BucketParentCapabilityResolver(RootCredential rootCredential, string bucket)
    {
        ArgumentNullException.ThrowIfNull(rootCredential);
        if (string.IsNullOrWhiteSpace(bucket)) throw new ArgumentException("A bucket is required.", nameof(bucket));
        var username = rootCredential.Username ?? throw new ArgumentException("Root credential requires a username.", nameof(rootCredential));
        var options = rootCredential.S3 ?? new S3RootOptions();
        var config = new AmazonS3Config
        {
            ServiceURL = new UriBuilder(options.Scheme, rootCredential.Host, rootCredential.Port).Uri.ToString(),
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region,
        };
        _client = new AmazonS3Client(username, rootCredential.Password, config);
        _bucket = bucket;
    }

    public async Task<bool> IsAvailableAsync(ParentContext parent, string contract, string source, CancellationToken cancellationToken)
    {
        if (contract != "IArchive") return false;
        var buckets = await _client.ListBucketsAsync(new ListBucketsRequest(), cancellationToken);
        // Buckets is null (not an empty list) when the account has zero buckets at all - the same
        // AWSSDK/MinIO XML-deserialization quirk S3ResourceProvider.BucketExistsAsync already
        // works around.
        return (buckets.Buckets ?? []).Any(b => string.Equals(b.BucketName, _bucket, StringComparison.Ordinal));
    }

    public void Dispose() => _client.Dispose();
}
