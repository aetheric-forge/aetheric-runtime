namespace Aetheric.Provisioning.Engine;

/// <summary>The complete, private configuration contract for each hosted module.</summary>
public static class WebModuleConsumerProfiles
{
    public static string Section(string module) => module switch
    {
        "library" => "Library", "archive" => "Archive", "decisions" => "Decisions",
        _ => throw new ArgumentException("Unknown web module.")
    };

    public static HashSet<string> RequiredSettings(string module)
    {
        var section = Section(module);
        var keys = new HashSet<string>(StringComparer.Ordinal) { section + ":Enabled" };
        void Add(string prefix, params string[] names)
        { foreach (var name in names) keys.Add(prefix + ":" + name); }
        if (module is "library" or "decisions")
            Add(section + ":MongoDb", "Host", "Port", "DatabaseName", "Username", "Password", "AuthenticationDatabase");
        if (module is "archive" or "decisions")
            Add(section + ":S3", "ServiceUrl", "AccessKey", "SecretKey", "BucketName", "ForcePathStyle", "AuthenticationRegion");
        if (module == "decisions")
        {
            Add("Decisions:Owner", "DeploymentId", "Environment");
            Add("Organization", "Id", "DisplayName", "MemberGroupId", "MaintainerGroupId");
            Add("Decisions:Directory:Keycloak", "Authority", "Realm", "ClientId", "ClientSecret");
            Add("Decisions:RabbitMq", "Host", "Port", "Ssl", "Username", "Password", "VirtualHost");
        }
        return keys;
    }
}
