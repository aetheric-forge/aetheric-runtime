namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;

public interface IExternalIdentity
{
    IExternalIdentityReference Reference { get; }
    string? DisplayName { get; }
    bool IsEnabled { get; }
    IReadOnlyDictionary<string, string> Properties { get; }
}
