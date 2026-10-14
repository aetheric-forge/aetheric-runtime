using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Aetheric.Provisioning.Worker;
using Amazon.S3;
using Amazon.S3.Model;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class SeaweedFsConsumerTests
{
    [Fact]
    public async Task Stable_key_and_bucket_policy_are_idempotent_and_existing_unowned_users_are_rejected()
    {
        var handler = new IamHandler();
        using var http = new HttpClient(handler);
        var provisioner = new SeaweedFsConsumerCredentials(http);
        var root = new RootCredentialPayload("archive.example", 443, "root", "root-password");
        await provisioner.EnsureAsync(root, "web12345", "consumer-secret", "one-bucket", default);
        await provisioner.EnsureAsync(root, "web12345", "consumer-secret", "one-bucket", default);
        Assert.Equal(1, handler.CreatedUsers);
        Assert.Equal(1, handler.CreatedKeys);
        Assert.DoesNotContain("root-password", handler.Policy);
        using var policy = JsonDocument.Parse(handler.Policy!);
        Assert.All(policy.RootElement.GetProperty("Statement").EnumerateArray(), statement =>
            Assert.All(statement.GetProperty("Resource").EnumerateArray(), resource => Assert.StartsWith("arn:aws:s3:::one-bucket", resource.GetString())));
        handler.Path = "/unowned/";
        var ex = await Assert.ThrowsAsync<ConsumerProvisioningException>(() => provisioner.EnsureAsync(root, "web12345", "consumer-secret", "one-bucket", default));
        Assert.Equal("consumer.archive_identity_conflict", ex.Code);
        Assert.Equal(2, handler.PoliciesWritten);
    }

    [SeaweedFact]
    public async Task Real_SeaweedFS_key_can_read_write_its_bucket_and_cannot_access_another_bucket()
    {
        var endpoint = new Uri(Environment.GetEnvironmentVariable("PROVISIONING_CONSUMER_TEST_SEAWEED")!);
        var root = new RootCredentialPayload(endpoint.Host, endpoint.Port, "consumer-test-admin", "consumer-test-root-password") { Scheme = endpoint.Scheme };
        var suffix = Guid.NewGuid().ToString("N");
        var bucket = "consumer-" + suffix;
        var other = "other-" + suffix;
        var config = new AmazonS3Config { ServiceURL = endpoint.AbsoluteUri, ForcePathStyle = true, AuthenticationRegion = "us-east-1" };
        using var admin = new AmazonS3Client(root.Username, root.Password, config);
        using var http = new HttpClient();
        await admin.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        await admin.PutBucketAsync(new PutBucketRequest { BucketName = other });
        var provisioner = new SeaweedFsConsumerCredentials(http);
        await provisioner.EnsureAsync(root, "web" + suffix, "consumer-test-scoped-password", bucket, default);
        await provisioner.EnsureAsync(root, "web" + suffix, "consumer-test-scoped-password", bucket, default);
        using var consumer = new AmazonS3Client("web" + suffix, "consumer-test-scoped-password", config);
        await consumer.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = "existing", ContentBody = "preserved" });
        using var read = await consumer.GetObjectAsync(bucket, "existing");
        using var reader = new StreamReader(read.ResponseStream);
        Assert.Equal("preserved", await reader.ReadToEndAsync());
        var denied = await Assert.ThrowsAsync<AmazonS3Exception>(() => consumer.PutObjectAsync(new PutObjectRequest { BucketName = other, Key = "forbidden", ContentBody = "no" }));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await consumer.DeleteObjectAsync(bucket, "existing");
        await admin.DeleteBucketAsync(bucket);
        await admin.DeleteBucketAsync(other);
    }

    private sealed class SeaweedFactAttribute : FactAttribute
    {
        public SeaweedFactAttribute()
        { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PROVISIONING_CONSUMER_TEST_SEAWEED"))) Skip = "Requires isolated SeaweedFS."; }
    }

    private sealed class IamHandler : HttpMessageHandler
    {
        public string? Path;
        public string? Policy;
        public int CreatedUsers, CreatedKeys, PoliciesWritten;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Contains("/us-east-1/iam/aws4_request", request.Headers.GetValues("Authorization").Single());
            var fields = (await request.Content!.ReadAsStringAsync(ct)).Split('&').Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair[1]));
            var status = HttpStatusCode.OK;
            var body = "<Response/>";
            switch (fields["Action"])
            {
                case "GetUser":
                    if (Path is null) { status = HttpStatusCode.NotFound; body = "<ErrorResponse><Error><Code>NoSuchEntity</Code></Error></ErrorResponse>"; }
                    else body = new XElement("GetUserResponse", new XElement("User", new XElement("Path", Path))).ToString();
                    break;
                case "CreateUser": Path = "/"; CreatedUsers++; break;
                case "TagUser": Path = fields["Tags.member.1.Value"]; break;
                case "ListUserTags": body = new XElement("ListUserTagsResponse", new XElement("Tags", new XElement("member", new XElement("Key", "aetheric-deployment-key"), new XElement("Value", Path)))).ToString(); break;
                case "PutUserPolicy": Policy = fields["PolicyDocument"]; PoliciesWritten++; break;
                case "ListAccessKeys": body = CreatedKeys == 0 ? "<ListAccessKeysResponse/>" : "<ListAccessKeysResponse><AccessKeyId>web12345</AccessKeyId></ListAccessKeysResponse>"; break;
                case "CreateAccessKey": Assert.Equal("consumer-secret", fields["SecretAccessKey"]); CreatedKeys++; break;
                default: throw new InvalidOperationException("Unexpected IAM action.");
            }
            return new(status) { Content = new StringContent(body) };
        }
    }
}
