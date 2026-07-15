using AethericForge.Runtime.Abstractions.Interfaces.Identity.Claims;

namespace AethericForge.Runtime.Models.Identity.Primitives;

public record IdentityClaim : IIdentityClaim
{
    private const int MaxTypeLength = 128;
    private const int MaxValueLength = 2048;
    private const int MaxIssuerLength = 256;

    public IdentityClaim(
        string type,
        string value,
        string? issuer = null,
        DateTimeOffset? issuedAtUtc = null,
        DateTimeOffset? expiresAtUtc = null)
    {
        Type = NormalizeRequired(type, nameof(type), MaxTypeLength);
        Value = NormalizeRequired(value, nameof(value), MaxValueLength);
        Issuer = NormalizeOptional(issuer, nameof(issuer), MaxIssuerLength);
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string Type { get; }
    public string Value { get; }
    public string? Issuer { get; }
    public DateTimeOffset? IssuedAtUtc { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }

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
