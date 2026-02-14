using MongoDB.Bson;
using MongoDB.Driver;
using AethericForge.Runtime.Repo.Abstractions;
using VyThread = AethericForge.Runtime.Model.Threads.Thread;
using AethericForge.Runtime.Model.Threads;

namespace AethericForge.Runtime.Repo.Backends;

// Minimal MongoDB implementation of IRepo.
// Stores all Thread-derived types in a single collection with a discriminator field "_t".
public class MongoRepo : IRepo
{
    private readonly IMongoCollection<BsonDocument> _col;

    public MongoRepo(string mongoUri, string databaseName = "parallel_you", string collectionName = "threads")
    {
        var db = new MongoClient(mongoUri).GetDatabase(databaseName);
        _col = db.GetCollection<BsonDocument>(collectionName);
    }

    public IList<VyThread> List(FilterSpec filter)
    {
        var fb = Builders<BsonDocument>.Filter;
        var filters = new List<FilterDefinition<BsonDocument>>();

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = filter.Text.Trim();
            // Case-insensitive contains on title
            filters.Add(fb.Regex("title", new BsonRegularExpression(text, "i")));
        }

        if (filter.Archived is not null)
        {
            filters.Add(fb.Eq("archived", filter.Archived.Value));
        }

        if (filter.Types is { Count: > 0 })
        {
            var names = filter.Types.Select(TypeToDiscriminator).ToArray();
            filters.Add(fb.In("_t", names));
        }

        var mongoFilter = filters.Count switch
        {
            0 => FilterDefinition<BsonDocument>.Empty,
            1 => filters[0],
            _ => fb.And(filters)
        };

        var docs = _col.Find(mongoFilter).ToList();
        return docs.Select(Deserialize).Select(t => t.Clone()).ToList();
    }

    public VyThread Get(string itemId)
    {
        var doc = _col.Find(Builders<BsonDocument>.Filter.Eq("_id", itemId)).FirstOrDefault();
        if (doc is null) throw new KeyNotFoundException($"No thread with id '{itemId}'");
        return Deserialize(doc).Clone();
    }

    public string Upsert(VyThread item)
    {
        var doc = Serialize(item);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", item.Id);
        _col.ReplaceOne(filter, doc, new ReplaceOptions { IsUpsert = true });
        return item.Id;
    }

    public bool Delete(string itemId)
    {
        var res = _col.DeleteOne(Builders<BsonDocument>.Filter.Eq("_id", itemId));
        return res.DeletedCount > 0;
    }

    public void Clear()
    {
        _col.DeleteMany(FilterDefinition<BsonDocument>.Empty);
    }

    private static string TypeToDiscriminator(Type t)
    {
        if (t == typeof(Domain)) return nameof(Domain);
        if (t == typeof(Saga)) return nameof(Saga);
        if (t == typeof(Story)) return nameof(Story);
        // Fallback to simple name
        return t.Name;
    }

    private static BsonDocument Serialize(VyThread t)
    {
        var doc = new BsonDocument
        {
            ["_id"] = t.Id,
            ["title"] = t.Title,
            ["priority"] = t.Priority,
            ["quantum"] = t.Quantum,
            ["archived"] = t.Archived,
            ["createdAt"] = t.CreatedAt.HasValue ? (BsonValue) t.CreatedAt.Value : BsonNull.Value,
            ["updatedAt"] = t.UpdatedAt.HasValue ? (BsonValue) t.UpdatedAt.Value : BsonNull.Value,
            ["archivedAt"] = t.ArchivedAt.HasValue ? (BsonValue) t.ArchivedAt.Value : BsonNull.Value,
        };

        switch (t)
        {
            case Domain d:
                doc["_t"] = nameof(Domain);
                if (d.Description != null) doc["description"] = d.Description;
                break;
            case Saga s:
                doc["_t"] = nameof(Saga);
                doc["domainId"] = s.DomainId;
                break;
            case Story st:
                doc["_t"] = nameof(Story);
                doc["sagaId"] = st.SagaId;
                doc["dueAt"] = st.DueAt.HasValue ? (BsonValue) st.DueAt.Value : BsonNull.Value;
                doc["energy"] = (int)st.Energy;
                doc["storyState"] = (int)st.StoryState;
                break;
            default:
                doc["_t"] = t.GetType().Name;
                break;
        }

        return doc;
    }

    private static VyThread Deserialize(BsonDocument doc)
    {
        var type = doc.GetValue("_t", BsonValue.Create("Thread")).AsString;

        string id = doc["_id"].AsString;
        string title = doc.GetValue("title", "").AsString;
        int priority = doc.GetValue("priority", 0).ToInt32();
        string quantum = doc.GetValue("quantum", "").AsString;
        bool archived = doc.GetValue("archived", false).ToBoolean();
        DateTime? createdAt = doc.Contains("createdAt") && !doc["createdAt"].IsBsonNull ? doc["createdAt"].ToUniversalTime() : null;
        DateTime? updatedAt = doc.Contains("updatedAt") && !doc["updatedAt"].IsBsonNull ? doc["updatedAt"].ToUniversalTime() : null;
        DateTime? archivedAt = doc.Contains("archivedAt") && !doc["archivedAt"].IsBsonNull ? doc["archivedAt"].ToUniversalTime() : null;

        return type switch
        {
            nameof(Domain) => new Domain(
                id: id,
                title: title,
                priority: priority,
                quantum: quantum,
                description: doc.GetValue("description", BsonNull.Value).IsBsonNull ? null : doc["description"].AsString,
                archived: archived,
                createdAt: createdAt,
                updatedAt: updatedAt,
                archivedAt: archivedAt
            ),
            nameof(Saga) => new Saga(
                id: id,
                domainId: doc.GetValue("domainId", "").AsString,
                title: title,
                priority: priority,
                quantum: quantum,
                archived: archived,
                createdAt: createdAt,
                updatedAt: updatedAt,
                archivedAt: archivedAt
            ),
            nameof(Story) => new Story(
                id: id,
                sagaId: doc.GetValue("sagaId", "").AsString,
                title: title,
                priority: priority,
                quantum: quantum,
                energy: (EnergyBand) doc.GetValue("energy", 0).ToInt32(),
                state: (StoryState) doc.GetValue("storyState", 0).ToInt32(),
                archived: archived,
                dueAt: doc.Contains("dueAt") && !doc["dueAt"].IsBsonNull ? doc["dueAt"].ToUniversalTime() : null,
                createdAt: createdAt,
                updatedAt: updatedAt,
                archivedAt: archivedAt
            ),
            _ => throw new InvalidOperationException($"Unknown thread type '{type}'")
        };
    }
}
