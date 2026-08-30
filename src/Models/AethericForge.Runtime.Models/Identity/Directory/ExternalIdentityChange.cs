using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;

namespace AethericForge.Runtime.Models.Identity.Directory;

public sealed record ExternalIdentityChange : IExternalIdentityChange
{
    public ExternalIdentityChange(
        IExternalIdentity? previous,
        IExternalIdentity current,
        DateTimeOffset observedAtUtc)
    {
        Current = current ?? throw new ArgumentNullException(nameof(current));
        if (previous is not null && !ReferencesMatch(previous.Reference, current.Reference))
        {
            throw new ArgumentException("Previous and current observations must describe the same identity.", nameof(previous));
        }

        Previous = previous;
        ObservedAtUtc = observedAtUtc;
        ChangedProperties = FindChanges(previous, current);
    }

    public IExternalIdentity? Previous { get; }
    public IExternalIdentity Current { get; }
    public IReadOnlyCollection<string> ChangedProperties { get; }
    public DateTimeOffset ObservedAtUtc { get; }

    private static IReadOnlyCollection<string> FindChanges(
        IExternalIdentity? previous,
        IExternalIdentity current)
    {
        if (previous is null)
        {
            return ["identity"];
        }

        var changes = new List<string>();
        if (!string.Equals(previous.DisplayName, current.DisplayName, StringComparison.Ordinal))
        {
            changes.Add("displayName");
        }

        if (previous.IsEnabled != current.IsEnabled)
        {
            changes.Add("isEnabled");
        }

        var keys = previous.Properties.Keys
            .Concat(current.Properties.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            previous.Properties.TryGetValue(key, out var previousValue);
            current.Properties.TryGetValue(key, out var currentValue);
            if (!string.Equals(previousValue, currentValue, StringComparison.Ordinal))
            {
                changes.Add(key);
            }
        }

        return changes;
    }

    private static bool ReferencesMatch(
        IExternalIdentityReference left,
        IExternalIdentityReference right) =>
        string.Equals(left.Provider, right.Provider, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Realm, right.Realm, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.SubjectId, right.SubjectId, StringComparison.Ordinal);
}
