namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Claims;

public interface IIdentityAttestation : IIdentityClaim
{
    byte[] Signature { get; }
    string Algorithm { get; }
    string? KeyId { get; }
}