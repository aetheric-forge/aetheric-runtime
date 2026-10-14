using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.Worker;

public interface IArchiveConsumerCredentials
{
    Task EnsureAsync(RootCredentialPayload root, string accessKey, string secretKey, string bucket, CancellationToken ct);
}

/// <summary>Uses SeaweedFS's embedded IAM API to persist a bucket-scoped identity and stable access key.</summary>
public sealed class SeaweedFsConsumerCredentials(HttpClient http) : IArchiveConsumerCredentials
{
    internal static string Policy(string bucket)
    {
        if (!Regex.IsMatch(bucket, "\\A[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]\\z"))
            throw new ArgumentException("Invalid archive bucket.");
        return JsonSerializer.Serialize(new
        {
            Version = "2012-10-17", Statement = new object[]
            {
                new { Effect = "Allow", Action = new[] { "s3:ListBucket", "s3:GetBucketLocation" }, Resource = new[] { "arn:aws:s3:::" + bucket } },
                new { Effect = "Allow", Action = new[] { "s3:GetObject", "s3:PutObject", "s3:DeleteObject", "s3:AbortMultipartUpload" }, Resource = new[] { "arn:aws:s3:::" + bucket + "/*" } }
            }
        });
    }

    public async Task EnsureAsync(RootCredentialPayload root, string accessKey, string secretKey, string bucket, CancellationToken ct)
    {
        var policy = Policy(bucket);
        if (!Regex.IsMatch(accessKey, "\\A[a-zA-Z0-9]{4,128}\\z") || secretKey.Length is < 8 or > 128
            || root.Scheme is not (null or "http" or "https") || Uri.CheckHostName(root.Host) == UriHostNameType.Unknown)
            throw new ArgumentException("Invalid SeaweedFS consumer identity or endpoint.");
        var endpoint = new UriBuilder(root.Scheme ?? "https", root.Host, root.Port).Uri;
        var user = "aetheric-" + accessKey;
        const string ownerTag = "aetheric-deployment-key";
        var existing = await Call("GetUser", new() { ["UserName"] = user }, missingAllowed: true);
        if (existing is null)
        {
            await Call("CreateUser", new() { ["UserName"] = user });
            await Call("TagUser", new() { ["UserName"] = user, ["Tags.member.1.Key"] = ownerTag, ["Tags.member.1.Value"] = accessKey });
        }
        var tags = await Call("ListUserTags", new() { ["UserName"] = user });
        if (!tags!.Descendants().Any(node => node.Elements().Any(key => key.Name.LocalName == "Key" && key.Value == ownerTag)
            && node.Elements().Any(value => value.Name.LocalName == "Value" && value.Value == accessKey)))
            throw new ConsumerProvisioningException("consumer.archive_identity_conflict");
        await Call("PutUserPolicy", new() { ["UserName"] = user, ["PolicyName"] = "archive-bucket", ["PolicyDocument"] = policy });
        var keys = await Call("ListAccessKeys", new() { ["UserName"] = user });
        var existingKeys = keys!.Descendants().Where(node => node.Name.LocalName == "AccessKeyId").Select(node => node.Value).ToArray();
        if (existingKeys.Any(key => key != accessKey))
            throw new ConsumerProvisioningException("consumer.archive_key_conflict");
        if (!existingKeys.Contains(accessKey))
            await Call("CreateAccessKey", new() { ["UserName"] = user, ["AccessKeyId"] = accessKey, ["SecretAccessKey"] = secretKey });

        async Task<XDocument?> Call(string action, Dictionary<string, string> values, bool missingAllowed = false)
        {
            values["Action"] = action; values["Version"] = "2010-05-08";
            using var content = new FormUrlEncodedContent(values);
            var payload = await content.ReadAsByteArrayAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            Sign(request, payload, root.Username, root.Password, DateTimeOffset.UtcNow);
            using var response = await http.SendAsync(request, ct);
            // Never include IAM response bodies in errors: access-key responses contain private credentials.
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            XDocument document;
            try { document = XDocument.Parse(Encoding.UTF8.GetString(bytes)); }
            catch (System.Xml.XmlException) { throw new ConsumerProvisioningException("consumer.archive_iam_invalid_response"); }
            if (!response.IsSuccessStatusCode)
            {
                var code = document.Descendants().FirstOrDefault(node => node.Name.LocalName == "Code")?.Value;
                if (missingAllowed && code == "NoSuchEntity") return null;
                throw new ConsumerProvisioningException("consumer.archive_iam_failed");
            }
            return document;
        }
    }

    internal static void Sign(HttpRequestMessage request, byte[] payload, string accessKey, string secretKey, DateTimeOffset now)
    {
        var date = now.UtcDateTime.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var timestamp = now.UtcDateTime.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture);
        var hash = Convert.ToHexStringLower(SHA256.HashData(payload));
        request.Headers.Host = request.RequestUri!.Authority;
        request.Headers.TryAddWithoutValidation("x-amz-date", timestamp);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", hash);
        const string headers = "host;x-amz-content-sha256;x-amz-date";
        var canonical = "POST\n/\n\nhost:" + request.Headers.Host + "\nx-amz-content-sha256:" + hash
            + "\nx-amz-date:" + timestamp + "\n\n" + headers + "\n" + hash;
        var scope = date + "/us-east-1/iam/aws4_request";
        var toSign = "AWS4-HMAC-SHA256\n" + timestamp + "\n" + scope + "\n"
            + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        byte[] Hmac(byte[] key, string value) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));
        var key = Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes("AWS4" + secretKey), date), "us-east-1"), "iam"), "aws4_request");
        var signature = Convert.ToHexStringLower(Hmac(key, toSign));
        request.Headers.TryAddWithoutValidation("Authorization", "AWS4-HMAC-SHA256 Credential=" + accessKey + "/" + scope
            + ", SignedHeaders=" + headers + ", Signature=" + signature);
    }
}
