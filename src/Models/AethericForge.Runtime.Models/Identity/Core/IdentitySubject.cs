using AethericForge.Runtime.Abstractions.Interfaces.Identity.Primitives;

namespace AethericForge.Runtime.Models.Identity.Core;

public sealed record IdentitySubject : IIdentitySubject
{
    private const int MaxSubjectIdLength = 256;
    private const int MaxDisplayNameLength = 256;

    public IdentitySubject(
        string subjectId,
        IdentityScheme scheme,
        string? displayName = null,
        IdentityState state = IdentityState.Active,
        IEnumerable<IIdentityClaim>? claims = null)
    {
        SubjectId = NormalizeRequired(subjectId, nameof(subjectId), MaxSubjectIdLength);
        Scheme = scheme;
        DisplayName = NormalizeOptional(displayName, nameof(displayName), MaxDisplayNameLength);
        State = state;
        Claims = claims?.ToArray() ?? [];
    }

    public string SubjectId { get; }
    public IdentityScheme Scheme { get; }
    public string? DisplayName { get; }
    public IdentityState State { get; }
    public IReadOnlyCollection<IIdentityClaim> Claims { get; }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return NormalizeLength(value.Trim(), parameterName, maxLength);
    }

    private static string? NormalizeOptional(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeLength(value.Trim(), parameterName, maxLength);
    }

    private static string NormalizeLength(string value, string parameterName, int maxLength)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Value must be {maxLength} characters or fewer.");
        }

        return value;
    }
}
