namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Claims;

public interface IKnowledgeAttestation : IKnowledgeClaim
{
    byte[] Signature { get; }
    string Algorithm { get; }
    string? KeyId { get; }
}
