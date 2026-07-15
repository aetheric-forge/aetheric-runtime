namespace AethericForge.Runtime.Institutions.Abstractions.Models;

public sealed record InitialStateDefinition
{
    public InitialStateDefinition(IDictionary<string, object>? configuration = null)
    {
        Configuration = configuration?.ToDictionary(k => k.Key, v => v.Value) 
                        ?? new Dictionary<string, object>();
    }

    public IReadOnlyDictionary<string, object> Configuration { get; init; }
}