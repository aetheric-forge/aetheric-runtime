using AethericForge.Runtime.Abstractions.Interfaces.Identity.Claims;

namespace AethericForge.Runtime.Models.Identity.Primitives;

public sealed record IdentityAttestation : IdentityClaim, IIdentityAttestation
{
    public IdentityAttestation(
        string type,
        string value,
        byte[] signature,
        string algorithm,
        string? issuer = null,
        string? keyId = null,
        DateTimeOffset? issuedAtUtc = null,
        DateTimeOffset? expiresAtUtc = null)
        : base(type, value, issuer, issuedAtUtc, expiresAtUtc)
    {
        Signature = signature ?? throw new ArgumentNullException(nameof(signature));
        Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
        KeyId = keyId;
    }

    public byte[] Signature { get; }
    public string Algorithm { get; }
    public string? KeyId { get; }
}
