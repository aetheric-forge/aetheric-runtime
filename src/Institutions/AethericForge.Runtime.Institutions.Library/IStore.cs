namespace AethericForge.Runtime.Institutions.Library;

public interface IStore<TKey, TValue>
    where TKey : notnull
{
    Task SetAsync(TKey key, TValue value, CancellationToken ct = default);
    Task<TValue?> GetAsync(TKey key, CancellationToken ct = default);
    Task<bool> ExistsAsync(TKey key, CancellationToken ct = default);
    Task<bool> RemoveAsync(TKey key, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}
