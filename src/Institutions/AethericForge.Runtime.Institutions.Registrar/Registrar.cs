using System.Collections.Concurrent;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Identity.Primitives;

namespace AethericForge.Runtime.Institutions.Registrar;

public sealed class Registrar : IRegistrarInstitution
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ConcurrentDictionary<string, IPrincipalIdentity> _records = new(StringComparer.Ordinal);

    public Registrar(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
    }

    public Task<IPrincipalIdentity> RegisterAsync(
        IIdentitySubject subject,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ct.ThrowIfCancellationRequested();

        var key = CreateKey(subject);

        if (_records.TryGetValue(key, out var existing))
        {
            ThrowIfConflicting(existing.Subject, subject);
            return Task.FromResult(existing);
        }

        var principal = new PrincipalIdentity(subject);

        if (_records.TryAdd(key, principal))
        {
            return Task.FromResult<IPrincipalIdentity>(principal);
        }

        existing = _records[key];
        ThrowIfConflicting(existing.Subject, subject);
        return Task.FromResult(existing);
    }

    public Task<IPrincipalIdentity?> IdentifyAsync(
        IIdentitySubject subject,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(_records.GetValueOrDefault(CreateKey(subject)));
    }

    public async Task<IPrincipalIdentity?> AuthenticateAsync(
        IdentityCredentials credentials,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ct.ThrowIfCancellationRequested();

        var authenticated = await _authenticationService.AuthenticateAsync(credentials.Scheme, credentials.Values, ct);

        if (authenticated is null)
        {
            return null;
        }

        var registered = await IdentifyAsync(authenticated.Subject, ct);

        return registered is null
            ? null
            : new PrincipalIdentity(registered.Subject, isAuthenticated: true, MergeClaims(registered.Claims, authenticated.Claims));
    }

    public Task<bool> ExistsAsync(
        IIdentitySubject subject,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(_records.ContainsKey(CreateKey(subject)));
    }

    private static string CreateKey(IIdentitySubject subject)
    {
        return string.Join(':', subject.Scheme, subject.SubjectId);
    }

    private static void ThrowIfConflicting(IIdentitySubject existing, IIdentitySubject candidate)
    {
        if (existing.State != candidate.State || !ClaimsMatch(existing.Claims, candidate.Claims))
        {
            throw new InvalidOperationException(
                $"Identity subject '{candidate.SubjectId}' is already registered with conflicting details.");
        }
    }

    private static bool ClaimsMatch(
        IReadOnlyCollection<IIdentityClaim> left,
        IReadOnlyCollection<IIdentityClaim> right)
    {
        return left.Select(CreateClaimKey).Order(StringComparer.Ordinal).SequenceEqual(
            right.Select(CreateClaimKey).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private static IReadOnlyCollection<IIdentityClaim> MergeClaims(
        IReadOnlyCollection<IIdentityClaim> registeredClaims,
        IReadOnlyCollection<IIdentityClaim> authenticatedClaims)
    {
        return registeredClaims
            .Concat(authenticatedClaims)
            .GroupBy(CreateClaimKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static string CreateClaimKey(IIdentityClaim claim)
    {
        return string.Join(
            '\u001f',
            claim.Type,
            claim.Value,
            claim.Issuer ?? string.Empty,
            claim.IssuedAtUtc?.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            claim.ExpiresAtUtc?.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
    }
}
