using System.Text;
using AethericForge.Runtime.Abstractions.Interfaces.Storage;
using AethericForge.Runtime.Abstractions.Interfaces.Storage.Providers;
using AethericForge.Runtime.Models.Storage;
using AethericForge.Runtime.Services.Storage;

namespace AethericForge.Runtime.Tests;

public class StorageTests
{
    [Fact]
    public async Task StorageService_Routes_Operations_To_Reference_Store()
    {
        var primary = new RecordingStorageProvider("primary");
        var archive = new RecordingStorageProvider("archive");
        var service = new StorageService([primary, archive]);

        var reference = await service.PutAsync(
            "archive",
            "notes/one.txt",
            CreateStream("hello"),
            new StorageMetadata("text/plain"));

        Assert.Equal("archive", reference.Store);
        Assert.Equal("notes/one.txt", reference.Key);
        Assert.Equal(["put:notes/one.txt"], archive.Calls);
        Assert.Empty(primary.Calls);

        await service.ExistsAsync(reference);
        await service.StatAsync(reference);
        await service.OpenReadAsync(reference);
        await service.DeleteAsync(reference);

        Assert.Equal(
            ["put:notes/one.txt", "exists:notes/one.txt", "stat:notes/one.txt", "read:notes/one.txt", "delete:notes/one.txt"],
            archive.Calls);
    }

    [Fact]
    public async Task StorageService_Requires_A_Provider_For_The_Requested_Store()
    {
        var service = new StorageService([new RecordingStorageProvider("primary")]);
        var reference = new StorageReference("archive", "notes/one.txt");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ExistsAsync(reference));
    }

    [Fact]
    public void StorageMetadata_Normalizes_Values_And_Attributes()
    {
        var modified = new DateTimeOffset(2026, 7, 11, 9, 30, 0, TimeSpan.FromHours(-6));
        var metadata = new StorageMetadata(
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

    private static MemoryStream CreateStream(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value));
    }

    private sealed class RecordingStorageProvider : IStorageProvider
    {
        public RecordingStorageProvider(string store)
        {
            Store = store;
        }

        public string Store { get; }
        public List<string> Calls { get; } = [];

        public Task<IStorageReference> PutAsync(
            string key,
            Stream content,
            IStorageMetadata? metadata = null,
            CancellationToken ct = default)
        {
            Calls.Add($"put:{key}");
            return Task.FromResult<IStorageReference>(new StorageReference(Store, key));
        }

        public Task<Stream> OpenReadAsync(
            IStorageReference reference,
            CancellationToken ct = default)
        {
            Calls.Add($"read:{reference.Key}");
            return Task.FromResult<Stream>(CreateStream("stored"));
        }

        public Task<IStorageMetadata?> StatAsync(
            IStorageReference reference,
            CancellationToken ct = default)
        {
            Calls.Add($"stat:{reference.Key}");
            return Task.FromResult<IStorageMetadata?>(new StorageMetadata("text/plain"));
        }

        public Task<bool> ExistsAsync(
            IStorageReference reference,
            CancellationToken ct = default)
        {
            Calls.Add($"exists:{reference.Key}");
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(
            IStorageReference reference,
            CancellationToken ct = default)
        {
            Calls.Add($"delete:{reference.Key}");
            return Task.FromResult(true);
        }
    }
}
