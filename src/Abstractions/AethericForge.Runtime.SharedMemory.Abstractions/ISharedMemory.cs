namespace AethericForge.Runtime.SharedMemory.Abstractions;

public interface ISharedMemory
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, CancellationToken ct = default);
    Task<bool> RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    // Optionally: add SetIfNotExistsAsync, IncrementAsync etc. in the future.
}
