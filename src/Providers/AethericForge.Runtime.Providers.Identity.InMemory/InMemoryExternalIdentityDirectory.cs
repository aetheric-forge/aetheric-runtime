using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;
using AethericForge.Runtime.Models.Identity.Directory;

namespace AethericForge.Runtime.Providers.Identity.InMemory;

public sealed class InMemoryExternalIdentityDirectory : IExternalIdentityDirectory
{
    private readonly object _sync = new();
    private readonly Dictionary<string, IExternalIdentity> _identities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _groups = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _freshnessLifetime;
    private ExternalDirectoryStatus? _failureStatus;
    private string? _failureReason;

    public InMemoryExternalIdentityDirectory(
        string provider,
        string realm,
        TimeProvider? timeProvider = null,
        TimeSpan? freshnessLifetime = null)
    {
        Provider = NormalizeRequired(provider, nameof(provider));
        Realm = NormalizeRequired(realm, nameof(realm));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _freshnessLifetime = freshnessLifetime ?? TimeSpan.FromMinutes(1);

        if (_freshnessLifetime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(freshnessLifetime),
                freshnessLifetime,
                "Freshness lifetime cannot be negative.");
        }
    }

    public string Provider { get; }
    public string Realm { get; }

    public Task<IExternalDirectoryResult<IExternalIdentity>> GetIdentityAsync(
        IExternalIdentityReference identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var failure = Failure<IExternalIdentity>();
            if (failure is not null)
            {
                return Task.FromResult<IExternalDirectoryResult<IExternalIdentity>>(failure);
            }

            if (!BelongsToDirectory(identity.Provider, identity.Realm))
            {
                return Task.FromResult<IExternalDirectoryResult<IExternalIdentity>>(
                    ExternalDirectoryResult<IExternalIdentity>.Failure(
                        ExternalDirectoryStatus.Untrusted,
                        Now(),
                        "The identity reference belongs to another provider or realm."));
            }

            return Task.FromResult<IExternalDirectoryResult<IExternalIdentity>>(
                _identities.TryGetValue(identity.SubjectId, out var value)
                    ? Success(value)
                    : ExternalDirectoryResult<IExternalIdentity>.Failure(
                        ExternalDirectoryStatus.NotFound,
                        Now()));
        }
    }

    public Task<IExternalDirectoryResult<IReadOnlyCollection<IExternalGroupReference>>> GetGroupsAsync(
        IExternalIdentityReference identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var failure = Failure<IReadOnlyCollection<IExternalGroupReference>>();
            if (failure is not null)
            {
                return Task.FromResult<IExternalDirectoryResult<IReadOnlyCollection<IExternalGroupReference>>>(failure);
            }

            if (!BelongsToDirectory(identity.Provider, identity.Realm))
            {
                return Task.FromResult<IExternalDirectoryResult<IReadOnlyCollection<IExternalGroupReference>>>(
                    ExternalDirectoryResult<IReadOnlyCollection<IExternalGroupReference>>.Failure(
                        ExternalDirectoryStatus.Untrusted,
                        Now(),
                        "The identity reference belongs to another provider or realm."));
            }

            if (!_identities.ContainsKey(identity.SubjectId))
            {
                return Task.FromResult<IExternalDirectoryResult<IReadOnlyCollection<IExternalGroupReference>>>(
                    ExternalDirectoryResult<IReadOnlyCollection<IExternalGroupReference>>.Failure(
                        ExternalDirectoryStatus.NotFound,
                        Now()));
            }

            IReadOnlyCollection<IExternalGroupReference> groups = _groups
                .Where(group => group.Value.Contains(identity.SubjectId))
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => (IExternalGroupReference)new ExternalGroupReference(Provider, Realm, group.Key))
                .ToArray();

            return Task.FromResult<IExternalDirectoryResult<IReadOnlyCollection<IExternalGroupReference>>>(
                Success(groups));
        }
    }

    public Task<IExternalDirectoryResult<IReadOnlyCollection<IExternalIdentity>>> GetGroupMembersAsync(
        IExternalGroupReference group,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var failure = Failure<IReadOnlyCollection<IExternalIdentity>>();
            if (failure is not null)
            {
                return Task.FromResult<IExternalDirectoryResult<IReadOnlyCollection<IExternalIdentity>>>(failure);
            }

            if (!BelongsToDirectory(group.Provider, group.Realm))
            {
                return Task.FromResult<IExternalDirectoryResult<IReadOnlyCollection<IExternalIdentity>>>(
                    ExternalDirectoryResult<IReadOnlyCollection<IExternalIdentity>>.Failure(
                        ExternalDirectoryStatus.Untrusted,
                        Now(),
                        "The group reference belongs to another provider or realm."));
            }

            if (!_groups.TryGetValue(group.GroupId, out var subjectIds))
            {
                return Task.FromResult<IExternalDirectoryResult<IReadOnlyCollection<IExternalIdentity>>>(
                    ExternalDirectoryResult<IReadOnlyCollection<IExternalIdentity>>.Failure(
                        ExternalDirectoryStatus.NotFound,
                        Now()));
            }

            IReadOnlyCollection<IExternalIdentity> members = subjectIds
                .Select(subjectId => _identities[subjectId])
                .OrderBy(identity => identity.Reference.SubjectId, StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult<IExternalDirectoryResult<IReadOnlyCollection<IExternalIdentity>>>(
                Success(members));
        }
    }

    public IExternalIdentityChange AddOrUpdateIdentity(IExternalIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        EnsureLocal(identity.Reference.Provider, identity.Reference.Realm, nameof(identity));

        lock (_sync)
        {
            _identities.TryGetValue(identity.Reference.SubjectId, out var previous);
            _identities[identity.Reference.SubjectId] = identity;
            return new ExternalIdentityChange(previous, identity, Now());
        }
    }

    public void AddGroup(IExternalGroupReference group)
    {
        ArgumentNullException.ThrowIfNull(group);
        EnsureLocal(group.Provider, group.Realm, nameof(group));

        lock (_sync)
        {
            _groups.TryAdd(group.GroupId, new HashSet<string>(StringComparer.Ordinal));
        }
    }

    public void SetGroupMembers(IExternalGroupReference group, IEnumerable<IExternalIdentityReference> members)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(members);
        EnsureLocal(group.Provider, group.Realm, nameof(group));

        lock (_sync)
        {
            if (!_groups.ContainsKey(group.GroupId))
            {
                throw new ArgumentException("The group does not exist in this directory.", nameof(group));
            }

            var subjectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in members)
            {
                ArgumentNullException.ThrowIfNull(member);
                EnsureLocal(member.Provider, member.Realm, nameof(members));
                if (!_identities.ContainsKey(member.SubjectId))
                {
                    throw new ArgumentException($"Identity '{member.SubjectId}' does not exist.", nameof(members));
                }

                subjectIds.Add(member.SubjectId);
            }

            _groups[group.GroupId] = subjectIds;
        }
    }

    public void SimulateFailure(ExternalDirectoryStatus status, string? reason = null)
    {
        if (status is ExternalDirectoryStatus.Success or ExternalDirectoryStatus.NotFound)
        {
            throw new ArgumentException("Only provider-level failures can be simulated.", nameof(status));
        }

        lock (_sync)
        {
            _failureStatus = status;
            _failureReason = reason;
        }
    }

    public void Restore()
    {
        lock (_sync)
        {
            _failureStatus = null;
            _failureReason = null;
        }
    }

    private ExternalDirectoryResult<TValue> Success<TValue>(TValue value)
    {
        var observedAt = Now();
        return ExternalDirectoryResult<TValue>.Success(
            value,
            observedAt,
            observedAt.Add(_freshnessLifetime));
    }

    private ExternalDirectoryResult<TValue>? Failure<TValue>() =>
        _failureStatus is { } status
            ? ExternalDirectoryResult<TValue>.Failure(status, Now(), _failureReason)
            : null;

    private bool BelongsToDirectory(string provider, string realm) =>
        string.Equals(Provider, provider, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Realm, realm, StringComparison.OrdinalIgnoreCase);

    private void EnsureLocal(string provider, string realm, string parameterName)
    {
        if (!BelongsToDirectory(provider, realm))
        {
            throw new ArgumentException(
                "The reference belongs to another provider or realm.",
                parameterName);
        }
    }

    private DateTimeOffset Now() => _timeProvider.GetUtcNow();

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
