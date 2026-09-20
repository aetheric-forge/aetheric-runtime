using Aetheric.Provisioning.Engine;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Aetheric.Provisioning.MongoDb;

/// <summary>
/// Live-verifies a Library parent dependency ("ILibrary") - the MongoDB counterpart to
/// Aetheric.Provisioning.Keycloak's KeycloakRealmParentCapabilityResolver, with one difference:
/// Mongo creates databases lazily on first write, and MongoDbResourceProvider never writes any
/// data, so "does the database exist" is not a reliable signal - a database created by that
/// provider may never actually appear even after fully successful provisioning. Instead this
/// checks the scoped user ("{owningInstitutionId}-library") the owning level's own
/// MongoDbResourceProvider actually created - the same thing that provider itself treats as
/// ground truth for "AlreadyExists". Deliberately narrow - recognizes exactly one contract,
/// returns false for anything else - so composing it with the other systems' resolvers
/// (CompositeParentCapabilityResolver) is a plain OR: only the one whose contract matches ever
/// performs real I/O.
/// </summary>
public sealed class MongoDbLibraryParentCapabilityResolver : IParentCapabilityResolver, IDisposable
{
    private readonly MongoClient _client;
    private readonly string _scopedUser;

    public MongoDbLibraryParentCapabilityResolver(RootCredential rootCredential, string owningInstitutionId)
    {
        ArgumentNullException.ThrowIfNull(rootCredential);
        if (string.IsNullOrWhiteSpace(owningInstitutionId)) throw new ArgumentException("An owning institution id is required.", nameof(owningInstitutionId));
        var username = rootCredential.Username ?? throw new ArgumentException("Root credential requires a username.", nameof(rootCredential));
        var options = rootCredential.Mongo ?? new MongoRootOptions();
        var settings = new MongoClientSettings
        {
            Server = new MongoServerAddress(rootCredential.Host, rootCredential.Port),
            Credential = MongoCredential.CreateCredential(options.AuthDatabase, username, rootCredential.Password),
            DirectConnection = options.DirectConnection,
            ApplicationName = "aetheric-provisioning",
        };
        _client = new MongoClient(settings);
        _scopedUser = $"{owningInstitutionId}-library";
    }

    public async Task<bool> IsAvailableAsync(ParentContext parent, string contract, string source, CancellationToken cancellationToken)
    {
        if (contract != "ILibrary") return false;
        var admin = _client.GetDatabase("admin");
        var command = new BsonDocument { { "usersInfo", new BsonDocument { { "user", _scopedUser }, { "db", "admin" } } } };
        var result = await admin.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken);
        return result["users"].AsBsonArray.Count > 0;
    }

    public void Dispose() => _client.Dispose();
}
