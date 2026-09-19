using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aetheric.Provisioning.Engine;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Aetheric.Provisioning.MongoDb;

/// <summary>
/// Ensures a resource-scoped MongoDB user exists with readWrite on the target database. The
/// user is created against "admin" (matching how the host's own root credential authenticates,
/// via RootConnectionValidator's pattern), with its role scoped to the target database - Mongo
/// creates the database itself lazily on first write, so there's no separate "create database"
/// step. The host supplies root credentials for the target server - connections never enter a plan.
/// </summary>
public sealed class MongoDbResourceProvider : IResourceProvider, IDisposable
{
    private static readonly Regex DatabasePattern = new(@"\A[a-zA-Z0-9_-]{1,63}\z", RegexOptions.Compiled);

    private readonly MongoClient _client;

    public MongoDbResourceProvider(RootCredential rootCredential)
    {
        ArgumentNullException.ThrowIfNull(rootCredential);
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
    }

    public string Key => "mongodb";

    public ImmutableArray<ValidationIssue> Validate(ResourceRequirement resource, ResourceBinding binding)
    {
        var valid = resource.Ownership == "owned" && binding.Provider == Key
            && binding.Settings.TryGetValue("database", out var database) && DatabasePattern.IsMatch(database)
            && binding.Settings.Keys.All(k => k is "database")
            && binding.Secrets.IsEmpty;
        return valid ? [] : [new("mongodb.binding", resource.Id,
            "MongoDB requires an owned resource with only a database setting (1-63 characters: letters, digits, '_', '-'). Credentials are supplied by the host.")];
    }

    public async Task<ProviderResult> EnsureAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        if (!Validate(context.Resource, context.Binding).IsEmpty)
            throw new InvalidOperationException("Invalid MongoDB binding.");

        var database = context.Binding.Settings["database"];
        var scopedUser = $"{context.InstitutionId}-{context.Resource.Id}";
        var admin = _client.GetDatabase("admin");

        var alreadyExists = await UserExistsAsync(admin, scopedUser, cancellationToken);

        var secret = await context.Secrets.GetOrCreateAsync("mongodb", scopedUser, cancellationToken);
        var password = await context.Secrets.ReadAsync(secret, cancellationToken);

        // updateUser when the user already exists rather than re-running createUser (which
        // fails on a duplicate) - same password/roles either way, so this is a safe no-op repeat,
        // matching the idempotency the other providers give the same guarantee.
        var command = new BsonDocument
        {
            { alreadyExists ? "updateUser" : "createUser", scopedUser },
            { "pwd", password },
            { "roles", new BsonArray { new BsonDocument { { "role", "readWrite" }, { "db", database } } } },
        };
        await admin.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken);

        return new(alreadyExists, [secret]);
    }

    private static async Task<bool> UserExistsAsync(IMongoDatabase admin, string username, CancellationToken ct)
    {
        var command = new BsonDocument { { "usersInfo", new BsonDocument { { "user", username }, { "db", "admin" } } } };
        var result = await admin.RunCommandAsync<BsonDocument>(command, cancellationToken: ct);
        return result["users"].AsBsonArray.Count > 0;
    }

    public void Dispose() => _client.Dispose();
}
