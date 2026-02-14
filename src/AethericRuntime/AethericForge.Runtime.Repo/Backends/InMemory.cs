using VyThread = AethericForge.Runtime.Model.Threads.Thread;

using AethericForge.Runtime.Util;
using AethericForge.Runtime.Repo.Abstractions;

namespace AethericForge.Runtime.Repo.Backends;

public class InMemoryRepo : IRepo
{
    private readonly Dictionary<string, VyThread> _threads = [];

    public IList<VyThread> List(FilterSpec filter)
    {
        IEnumerable<VyThread> query = _threads.Values;

        // text filter (simple title contains; expand later if needed)
        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = filter.Text.Trim();
            query = query.Where(t => t.Title.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        // archived filter
        if (filter.Archived is not null)
        {
            bool wantArchived = filter.Archived.Value;
            query = query.Where(t => t.Archived == wantArchived);
        }

        // type filter (Domain/Saga/Story, etc.)
        if (filter.Types is { Count: > 0 })
        {
            var typeSet = filter.Types.ToHashSet();
            query = query.Where(t => typeSet.Contains(t.GetType()));
        }

        // deep copies out
        return [.. query.Select(t => t.Clone())];
    }

    public VyThread Get(string id) => _threads.GetOrThrow(id).Clone();

    public string Upsert(VyThread item)
    {
        var copy = item.Clone();
        _threads[copy.Id] = copy;
        return copy.Id;
    }

    public bool Delete(string id) => _threads.Remove(id);

    public void Clear() => _threads.Clear();
}
