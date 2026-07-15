using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Identity.Primitives;

namespace AethericForge.Runtime.Providers.Identity.InMemory;

public sealed class InMemoryIdentityProvider : IIdentityProvider
{
    private readonly object _sync = new();
    private readonly Dictionary<string, IIdentitySubject> _subjects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _passwords = new(StringComparer.Ordinal);

    public InMemoryIdentityProvider(string name, IdentityScheme scheme)
    {
        Name = NormalizeRequired(name, nameof(name));
        Scheme = scheme;
    }

    public string Name { get; }
    public IdentityScheme Scheme { get; }

    public Task<IIdentitySubject?> ResolveSubjectAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subjectId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            return Task.FromResult(_subjects.TryGetValue(subjectId, out var subject) ? subject : null);
        }
    }

    public Task<IPrincipalIdentity?> AuthenticateAsync(
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        cancellationToken.ThrowIfCancellationRequested();

        // Simple username/password authentication for the in-memory provider
        if (!credentials.TryGetValue("username", out var username) ||
            !credentials.TryGetValue("password", out var password))
        {
            return Task.FromResult<IPrincipalIdentity?>(null);
        }

        lock (_sync)
        {
            if (_passwords.TryGetValue(username, out var storedPassword) &&
                string.Equals(password, storedPassword, StringComparison.Ordinal) &&
                _subjects.TryGetValue(username, out var subject))
            {
                return Task.FromResult<IPrincipalIdentity?>(new PrincipalIdentity(subject, true));
            }
        }

        return Task.FromResult<IPrincipalIdentity?>(null);
    }

    public void AddSubject(IIdentitySubject subject, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(subject);

        lock (_sync)
        {
            _subjects[subject.SubjectId] = subject;
            if (password != null)
            {
                _passwords[subject.SubjectId] = password;
            }
        }
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
