using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;

namespace AethericForge.Runtime.Models.Identity.Directory;

public sealed record ExternalIdentityReference : IExternalIdentityReference
{
    public ExternalIdentityReference(string provider, string realm, string subjectId)
    {
        Provider = DirectoryValue.NormalizeRequired(provider, nameof(provider));
        Realm = DirectoryValue.NormalizeRequired(realm, nameof(realm));
        SubjectId = DirectoryValue.NormalizeRequired(subjectId, nameof(subjectId));
    }

    public string Provider { get; }
    public string Realm { get; }
    public string SubjectId { get; }
}
