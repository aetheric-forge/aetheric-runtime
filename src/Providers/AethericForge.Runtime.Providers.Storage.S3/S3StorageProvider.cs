using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using AethericForge.Runtime.Abstractions.Interfaces.Storage;
using AethericForge.Runtime.Abstractions.Interfaces.Storage.Providers;
using AethericForge.Runtime.Models.Storage;

namespace AethericForge.Runtime.Providers.Storage.S3;

public sealed class S3StorageProvider : IStorageProvider
{
    private const string UserMetadataPrefix = "x-amz-meta-";

    private readonly IAmazonS3 _client;
    private readonly string _bucketName;
    private readonly string? _keyPrefix;

    public S3StorageProvider(
        IAmazonS3 client,
        string store,
        string bucketName,
        string? keyPrefix = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Store = NormalizeRequired(store, nameof(store));
        _bucketName = NormalizeRequired(bucketName, nameof(bucketName));
        _keyPrefix = NormalizePrefix(keyPrefix);
    }

    public string Store { get; }

    public async Task<IStorageReference> PutAsync(
        string key,
        Stream content,
        IStorageMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();

        var normalizedKey = NormalizeRequired(key, nameof(key));
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = CreateS3Key(normalizedKey),
            InputStream = content,
            AutoCloseStream = false,
            ContentType = metadata?.ContentType
        };

        if (metadata?.ContentLength is { } contentLength)
        {
            request.Headers.ContentLength = contentLength;
        }

        if (metadata?.Attributes is { Count: > 0 } attributes)
        {
            foreach (var (attributeKey, value) in attributes)
            {
                request.Metadata[CreateUserMetadataKey(attributeKey)] = value;
            }
        }

        await _client.PutObjectAsync(request, ct);

        return new StorageReference(Store, normalizedKey);
    }

    public async Task<Stream> OpenReadAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        try
        {
            var response = await _client.GetObjectAsync(_bucketName, CreateS3Key(reference.Key), ct);
            return new S3ObjectStream(response);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException(
                $"Storage object '{reference.Store}:{reference.Key}' was not found.",
                reference.Key,
                ex);
        }
    }

    public async Task<IStorageMetadata?> StatAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        try
        {
            var response = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = CreateS3Key(reference.Key)
            }, ct);

            return CreateMetadata(response);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        return await StatAsync(reference, ct) is not null;
    }

    public async Task<bool> DeleteAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        if (!await ExistsAsync(reference, ct))
        {
            return false;
        }

        await _client.DeleteObjectAsync(_bucketName, CreateS3Key(reference.Key), ct);
        return true;
    }

    private void EnsureOwns(IStorageReference reference)
    {
        if (!string.Equals(Store, reference.Store, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Provider store '{Store}' cannot handle reference for store '{reference.Store}'.");
        }
    }

    private string CreateS3Key(string key)
    {
        var normalizedKey = NormalizeRequired(key, nameof(key)).TrimStart('/');

        return _keyPrefix is null
            ? normalizedKey
            : $"{_keyPrefix}/{normalizedKey}";
    }

    private static StorageMetadata CreateMetadata(GetObjectMetadataResponse response)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var metadataKey in response.Metadata.Keys)
        {
            attributes[NormalizeUserMetadataKey(metadataKey)] = response.Metadata[metadataKey];
        }

        return new StorageMetadata(
            response.Headers.ContentType,
            response.Headers.ContentLength,
            response.ETag,
            ToUtcDateTimeOffset(response.LastModified),
            attributes);
    }

    private static DateTimeOffset? ToUtcDateTimeOffset(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        var utc = value.Value.ToUniversalTime();
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizePrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Trim('/');
        return normalized.Length == 0 ? null : normalized;
    }

    private static string CreateUserMetadataKey(string key)
    {
        var normalized = NormalizeRequired(key, nameof(key));

        return normalized.StartsWith(UserMetadataPrefix, StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"{UserMetadataPrefix}{normalized}";
    }

    private static string NormalizeUserMetadataKey(string key)
    {
        return key.StartsWith(UserMetadataPrefix, StringComparison.OrdinalIgnoreCase)
            ? key[UserMetadataPrefix.Length..]
            : key;
    }

    private sealed class S3ObjectStream : Stream
    {
        private readonly GetObjectResponse _response;
        private readonly Stream _inner;

        public S3ObjectStream(GetObjectResponse response)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response));
            _inner = response.ResponseStream;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return await _inner.ReadAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
