using Amazon.S3;
using Amazon.S3.Model;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Models.Archive;
using AethericForge.Runtime.Providers.Archive.S3;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Archive.S3;

public class S3ArchiveProviderTests
{
    private readonly Mock<IAmazonS3> _mockS3;
    private readonly S3ArchiveProvider _provider;
    private const string StoreName = "test-store";
    private const string BucketName = "test-bucket";

    public S3ArchiveProviderTests()
    {
        _mockS3 = new Mock<IAmazonS3>();
        _provider = new S3ArchiveProvider(_mockS3.Object, StoreName, BucketName);
    }

    [Fact]
    public void Store_ShouldReturnStoreName()
    {
        Assert.Equal(StoreName, _provider.Store);
    }

    [Fact]
    public async Task PutAsync_ShouldCallPutObject()
    {
        // Arrange
        var key = "test-key";
        var content = "test-content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var ct = CancellationToken.None;

        _mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), ct))
            .ReturnsAsync(new PutObjectResponse());

        // Act
        await _provider.PutAsync(key, stream, ct: ct);

        // Assert
        _mockS3.Verify(x => x.PutObjectAsync(It.Is<PutObjectRequest>(r => 
            r.BucketName == BucketName && 
            r.Key == key), ct), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldCallDeleteObject()
    {
        // Arrange
        var key = "test-key";
        var reference = new ArchiveReference(StoreName, key);
        var ct = CancellationToken.None;

        _mockS3.Setup(x => x.DeleteObjectAsync(BucketName, key, ct))
            .ReturnsAsync(new DeleteObjectResponse());

        // Act
        var result = await _provider.DeleteAsync(reference, ct);

        // Assert
        Assert.True(result);
        _mockS3.Verify(x => x.DeleteObjectAsync(BucketName, key, ct), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenObjectExists()
    {
        // Arrange
        var key = "test-key";
        var reference = new ArchiveReference(StoreName, key);
        var ct = CancellationToken.None;

        _mockS3.Setup(x => x.GetObjectMetadataAsync(BucketName, key, ct))
            .ReturnsAsync(new GetObjectMetadataResponse());

        // Act
        var result = await _provider.ExistsAsync(reference, ct);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenObjectDoesNotExist()
    {
        // Arrange
        var key = "test-key";
        var reference = new ArchiveReference(StoreName, key);
        var ct = CancellationToken.None;

        _mockS3.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), ct))
            .ThrowsAsync(new AmazonS3Exception("Not Found", System.Net.HttpStatusCode.NotFound));

        // Act
        var result = await _provider.ExistsAsync(reference, ct);

        // Assert
        Assert.False(result);
    }
}
