using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;

namespace AethericForge.Runtime.Models.Identity.Directory;

public sealed record ExternalGroupReference : IExternalGroupReference
{
    public ExternalGroupReference(string provider, string realm, string groupId)
    {
        Provider = DirectoryValue.NormalizeRequired(provider, nameof(provider));
        Realm = DirectoryValue.NormalizeRequired(realm, nameof(realm));
        GroupId = DirectoryValue.NormalizeRequired(groupId, nameof(groupId));
    }

    public string Provider { get; }
    public string Realm { get; }
    public string GroupId { get; }
}
