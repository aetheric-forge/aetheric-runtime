namespace AethericForge.Runtime.Repo.Abstractions;

public interface IRepo<T> where T : IEntity
{
    Task<IReadOnlyList<T>> ListAsync(IFilterSpec filter, CancellationToken ct = default);
    Task<T> UpsertAsync(T item, CancellationToken ct = default); // returns the ID of the upserted object
    Task<T?> GetAsync(Guid itemId, CancellationToken ct = default);
    Task<T?> DeleteAsync(Guid itemId, CancellationToken ct = default); // deletes item specified by id, and returns true if item was deleted
    Task ClearAsync(CancellationToken ct = default);
}
