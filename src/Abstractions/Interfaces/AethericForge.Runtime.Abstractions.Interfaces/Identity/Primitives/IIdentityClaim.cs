namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Primitives;

public interface IIdentityClaim
{
    string Type { get; }
    string Value { get; }
    string? Issuer { get; }
    DateTimeOffset? IssuedAtUtc { get; }
    DateTimeOffset? ExpiresAtUtc { get; }
}
