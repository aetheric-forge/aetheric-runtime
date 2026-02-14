using VyThread = AethericForge.Runtime.Model.Threads.Thread;

namespace AethericForge.Runtime.Repo.Abstractions;

public class FilterSpec
(
    string? text = null,
    bool? archived = null,
    IList<Type>? types = null
)
{
    public string? Text { get; set; } = text;
    public bool? Archived { get; set; } = archived;

    public IList<Type>? Types { get; set; } = types;
}

public interface IRepo
{
    IList<VyThread> List(FilterSpec filter);
    string Upsert(VyThread item); // returns the ID of the upserted object
    VyThread Get(string itemId);
    bool Delete(string itemId); // deletes item specified by id, and returns true if item was deleted
    void Clear();
}
