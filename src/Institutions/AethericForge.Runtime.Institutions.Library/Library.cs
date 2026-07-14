using System.Collections.Concurrent;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;

namespace AethericForge.Runtime.Institutions.Library;

public sealed class Library(IInstitutionContext context) : ILibraryInstitution
{
    private readonly ConcurrentDictionary<string, Shelf> _shelves = new(StringComparer.Ordinal);

    public IReadOnlyCollection<Shelf> Shelves => _shelves.Values.OrderBy(shelf => shelf.Name, StringComparer.Ordinal).ToArray();

    public Shelf CreateShelf(string name)
    {
        var shelf = new Shelf(name);

        if (!_shelves.TryAdd(shelf.Name, shelf))
        {
            throw new InvalidOperationException($"Shelf '{shelf.Name}' already exists.");
        }

        return shelf;
    }

    public Shelf GetOrCreateShelf(string name)
    {
        var normalized = NormalizeName(name);
        return _shelves.GetOrAdd(normalized, shelfName => new Shelf(shelfName));
    }

    public Shelf GetShelf(string name)
    {
        var normalized = NormalizeName(name);

        return _shelves.TryGetValue(normalized, out var shelf)
            ? shelf
            : throw new KeyNotFoundException($"Shelf '{normalized}' was not found.");
    }

    public bool ContainsShelf(string name)
    {
        return _shelves.ContainsKey(NormalizeName(name));
    }

    public bool RemoveShelf(string name)
    {
        return _shelves.TryRemove(NormalizeName(name), out _);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Shelf name is required.", nameof(name));
        }

        return name.Trim();
    }

    public IInstitutionContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
