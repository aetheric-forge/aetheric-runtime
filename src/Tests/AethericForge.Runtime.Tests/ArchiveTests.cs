using System.Text;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Models.Archive;
using AethericForge.Runtime.Providers.Archive.MongoDb;
using AethericForge.Runtime.Services.Archive;

namespace AethericForge.Runtime.Tests;

public class ArchiveTests
{
    [Fact]
    public async Task ArchiveService_Routes_Operations_To_Reference_Store()
    {
        var primary = new RecordingArchiveProvider("primary");
        var archiveProvider = new RecordingArchiveProvider("archive");
        var service = new ArchiveService([primary, archiveProvider]);

        var reference = await service.PutAsync(
            "archive",
            "notes/one.txt",
            CreateStream("hello"),
            new ArchiveMetadata("text/plain"));

        Assert.Equal("archive", reference.Store);
        Assert.Equal("notes/one.txt", reference.Key);
        Assert.Equal(["put:notes/one.txt"], archiveProvider.Calls);
        Assert.Empty(primary.Calls);

        await service.ExistsAsync(reference);
        await service.StatAsync(reference);
        await service.OpenReadAsync(reference);
        await service.DeleteAsync(reference);

        Assert.Equal(
            ["put:notes/one.txt", "exists:notes/one.txt", "stat:notes/one.txt", "read:notes/one.txt", "delete:notes/one.txt"],
            archiveProvider.Calls);
    }

    [Fact]
    public async Task ArchiveService_Requires_A_Provider_For_The_Requested_Store()
    {
        var service = new ArchiveService([new RecordingArchiveProvider("primary")]);
        var reference = new ArchiveReference("archive", "notes/one.txt");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ExistsAsync(reference));
    }

    [Fact]
    public void ArchiveMetadata_Normalizes_Values_And_Attributes()
    {
        var modified = new DateTimeOffset(2026, 7, 11, 9, 30, 0, TimeSpan.FromHours(-6));
        var metadata = new ArchiveMetadata(
            " text/plain ",
            12,
            " abc ",
            modified,
            new Dictionary<string, string>
            {
                [" owner "] = "runtime"
            });

        Assert.Equal("text/plain", metadata.ContentType);
        Assert.Equal(12, metadata.ContentLength);
        Assert.Equal("abc", metadata.ETag);
        Assert.Equal(TimeSpan.Zero, metadata.LastModifiedUtc?.Offset);
        Assert.Equal("runtime", metadata.Attributes["owner"]);
    }

    [Fact]
    public async Task MongoDbArchiveProvider_Rejects_References_For_Other_Stores()
    {
        var provider = new MongoDbArchiveProvider(
            "mongodb://localhost:27017",
            "forge",
            "archive",
            "primary");
        var reference = new ArchiveReference("archive", "notes/one.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ExistsAsync(reference));
    }

    private static MemoryStream CreateStream(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value));
    }

    private sealed class RecordingArchiveProvider : IArchiveProvider
    {
        public RecordingArchiveProvider(string store)
        {
            Store = store;
        }

        public string Store { get; }
        public List<string> Calls { get; } = [];

        public Task<IArchiveReference> PutAsync(
            string key,
            Stream content,
            IArchiveMetadata? metadata = null,
            CancellationToken ct = default)
        {
            Calls.Add($"put:{key}");
            return Task.FromResult<IArchiveReference>(new ArchiveReference(Store, key));
        }

        public Task<Stream> OpenReadAsync(
            IArchiveReference reference,
            CancellationToken ct = default)
        {
            Calls.Add($"read:{reference.Key}");
            return Task.FromResult<Stream>(CreateStream("stored"));
        }

        public Task<IArchiveMetadata?> StatAsync(
            IArchiveReference reference,
            CancellationToken ct = default)
        {
            Calls.Add($"stat:{reference.Key}");
            return Task.FromResult<IArchiveMetadata?>(new ArchiveMetadata("text/plain"));
        }

        public Task<bool> ExistsAsync(
            IArchiveReference reference,
            CancellationToken ct = default)
        {
            Calls.Add($"exists:{reference.Key}");
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(
            IArchiveReference reference,
            CancellationToken ct = default)
        {
            Calls.Add($"delete:{reference.Key}");
            return Task.FromResult(true);
        }
    }
}
