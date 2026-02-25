namespace AethericForge.Runtime.Repo;

using System.Collections.Concurrent;
using Newtonsoft.Json;

using Abstractions;

public class InMemoryRepo<T> : IRepo<T> where T : IEntity
{
    private readonly ConcurrentDictionary<Guid, string> _entities = new();

    public Task<IReadOnlyList<T>> ListAsync(IFilterSpec filter, CancellationToken ct = default)
    {
        var query  = _entities.AsEnumerable();
        
        // Id filter (also available as GetAsync())
        if (filter.Id != null)
        {
            var id = filter.Id.Value;
            query = query.Where(kv => kv.Key == id);
        }
        
        var result = query
            .Select(kv => JsonConvert.DeserializeObject<T>(kv.Value) ?? 
                          throw new JsonException($"failed to deserialize object of type {typeof(T)}"))
            .ToList();

        return Task.FromResult<IReadOnlyList<T>>(result);
    }
    
    
    public Task<T> GetAsync(Guid id, CancellationToken ct = default)
    {
        var found = _entities.TryGetValue(id, out var json);
        
        // to coerce json to string below
        if (!found || json == null) throw new KeyNotFoundException($"No entity with id '{id}'");
        
        var entity = JsonConvert.DeserializeObject<T>(json) ?? throw new JsonException($"Failed to deserialize entity of type {typeof(T).Name}");
        
        return Task.FromResult(entity);
    }


    public Task<T> UpsertAsync(T item, CancellationToken ct = default)
    {
        _entities[item.Id] = JsonConvert.SerializeObject(item);
        var found = _entities.TryGetValue(item.Id,  out var json);
        if (!found || json == null) throw new JsonException($"Serialized JSON for entity {item.Id} was not found in the repo");
        var entity = JsonConvert.DeserializeObject<T>(json);
        if (entity == null) throw new JsonException($"Stored JSON for entity {item.Id} did not deserialize");
        return Task.FromResult(entity);
    }

    public Task<T> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var removed = _entities.Remove(id, out var json);
        if (!removed || json ==  null) return Task.FromResult(default(T));
        return Task.FromResult(JsonConvert.DeserializeObject<T>(json));
    }

    public Task ClearAsync(CancellationToken ct = default) 
    {
        _entities.Clear(); 
        return Task.CompletedTask;
    }
}