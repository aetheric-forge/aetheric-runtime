namespace AethericForge.Runtime.Repo;

using MongoDB.Driver;
using Abstractions;

// Minimal MongoDB implementation of IRepo.
public class MongoRepo<T> : IRepo<T> where T : IEntity
{
    private readonly IMongoCollection<T> _col;

    public MongoRepo(string mongoUri, string databaseName, string collectionName)
    {
        var db = new MongoClient(mongoUri).GetDatabase(databaseName);
        _col = db.GetCollection<T>(collectionName);
    }

    public Task<IReadOnlyList<T>> ListAsync(IFilterSpec filter, CancellationToken ct = default)
    {
        var fb = Builders<T>.Filter;
        var filters = new List<FilterDefinition<T>>();
        
        if (filter.Id != null) 
        {
           filters.Add(fb.Eq(e => e.Id, filter.Id));
        }

        var mongoFilter = filters.Count switch
        {
            0 => FilterDefinition<T>.Empty,
            1 => filters[0],
            _ => fb.And(filters)
        };

        var docs = (IReadOnlyList<T>) _col.Find(mongoFilter).ToList();
        return Task.FromResult(docs);
    }

    public Task<T> GetAsync(Guid itemId, CancellationToken ct = default)
    {
        var doc = _col.Find(Builders<T>.Filter.Eq(e => e.Id, itemId)).FirstOrDefault();
        if (doc is null) throw new KeyNotFoundException($"No entity with id '{itemId}'");
        return Task.FromResult(doc);
    }

    public Task<T> UpsertAsync(T item, CancellationToken ct = default)
    {
        var filter = Builders<T>.Filter.Eq(e => e.Id, item.Id);
        _col.ReplaceOne(filter, item, new ReplaceOptions { IsUpsert = true });
        return Task.FromResult(item);
    }

    public async Task<T> DeleteAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await GetAsync(itemId, ct);
        var res = _col.DeleteOne(Builders<T>.Filter.Eq(e => e.Id, itemId));
        return item;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        _col.DeleteMany(FilterDefinition<T>.Empty);
        return Task.CompletedTask;
    }
}
