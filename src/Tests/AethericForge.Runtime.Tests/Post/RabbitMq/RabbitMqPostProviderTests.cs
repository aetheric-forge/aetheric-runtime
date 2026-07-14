using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Models.Post;
using AethericForge.Runtime.Providers.Post.RabbitMq;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace AethericForge.Runtime.Tests.Post.RabbitMq;

public class RabbitMqPostProviderTests
{
    private readonly Mock<IConnectionFactory> _mockFactory;
    private readonly Mock<IConnection> _mockConnection;
    private readonly Mock<IChannel> _mockChannel;
    private readonly RabbitMqPostProvider _provider;
    private const string ProviderName = "test-provider";

    public RabbitMqPostProviderTests()
    {
        _mockFactory = new Mock<IConnectionFactory>();
        _mockConnection = new Mock<IConnection>();
        _mockChannel = new Mock<IChannel>();

        _mockFactory.Setup(x => x.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockConnection.Object);
        _mockConnection.Setup(x => x.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockChannel.Object);

        _provider = new RabbitMqPostProvider(ProviderName, _mockFactory.Object);
    }

    [Fact]
    public void Name_ShouldReturnProviderName()
    {
        Assert.Equal(ProviderName, _provider.Name);
    }

    [Fact]
    public async Task PublishAsync_ShouldPublishMessage()
    {
        // Arrange
        var mockEnvelope = new Mock<IPostEnvelope>();
        var mockReference = new Mock<IPostReference>();
        var mockMetadata = new Mock<IPostMetadata>();
        
        mockReference.Setup(x => x.Provider).Returns(ProviderName);
        mockReference.Setup(x => x.Domain).Returns("test-domain");
        mockReference.Setup(x => x.Address).Returns("test-address");
        mockReference.Setup(x => x.Contract).Returns("test-contract");

        mockMetadata.Setup(x => x.MessageId).Returns(Guid.NewGuid().ToString());
        mockMetadata.Setup(x => x.ProducedAtUtc).Returns(DateTimeOffset.UtcNow);
        mockMetadata.Setup(x => x.Attributes).Returns(new Dictionary<string, string>());

        mockEnvelope.Setup(x => x.Reference).Returns(mockReference.Object);
        mockEnvelope.Setup(x => x.Metadata).Returns(mockMetadata.Object);
        mockEnvelope.Setup(x => x.Content).Returns("test-content"u8.ToArray());

        var ct = CancellationToken.None;

        // Act
        await _provider.PublishAsync(mockEnvelope.Object, ct);

        // Assert
        _mockChannel.Verify(x => x.BasicPublishAsync(
            $"aetheric.post.{ProviderName}",
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<BasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            ct), Times.Once);
    }
}
