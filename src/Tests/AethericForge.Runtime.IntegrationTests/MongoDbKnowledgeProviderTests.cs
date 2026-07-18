using System.Text;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Identity.Primitives;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.References;
using AethericForge.Runtime.Models.Knowledge.Representations;
using AethericForge.Runtime.Providers.Knowledge.MongoDb;
using Xunit;

namespace AethericForge.Runtime.IntegrationTests;

public sealed class MongoDbKnowledgeProviderTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Round_trip_preserves_artifact_and_authoritative_reference()
    {
        var runId = Guid.NewGuid().ToString("N");
        var scheme = $"e2e-mongodb-{runId}";
        var provider = new MongoDbKnowledgeProvider(
            EnvironmentConfiguration.Require("AF_E2E_MONGODB_URI"),
            EnvironmentConfiguration.Get("AF_E2E_MONGODB_DATABASE", "aetheric_runtime_e2e"),
            $"knowledge_artifacts_{runId}",
            scheme,
            directConnection: bool.Parse(EnvironmentConfiguration.Get("AF_E2E_MONGODB_DIRECT_CONNECTION", "false")));
        var authority = new KnowledgeAuthority(
            new IdentitySubject("knowledge-owner", IdentityScheme.OpenIdConnect, "Knowledge Owner"),
            "IntegrationTest.Knowledge");
        var content = Encoding.UTF8.GetBytes($"knowledge representation {runId}");
        IKnowledgeRepresentation representation = new KnowledgeRepresentation(
            "text/plain",
            content.Length,
            _ => Task.FromResult<Stream>(new MemoryStream(content, writable: false)),
            encoding: "utf-8",
            language: "en");
        var lineage = new KnowledgeReference(scheme, "Artifact", "predecessor", "1.0.0");

        var stored = await provider.StoreArtifactAsync(
            new KnowledgeDescriptor("Mongo knowledge", summary: "Durable knowledge"),
            [representation],
            [lineage],
            authority);
        var retrieved = await provider.GetArtifactAsync(stored.Reference);

        Assert.NotNull(retrieved);
        Assert.Equal("Mongo knowledge", retrieved.Descriptor.Title);
        Assert.Equal("Durable knowledge", retrieved.Descriptor.Summary);
        Assert.Equal(lineage, Assert.Single(retrieved.Lineage));
        Assert.Equal(authority.Identity.SubjectId, retrieved.Authority?.Identity.SubjectId);
        Assert.Equal(authority.Context, retrieved.Authority?.Context);

        var retrievedRepresentation = Assert.Single(retrieved.Representations);
        await using var retrievedContent = await retrievedRepresentation.OpenStreamAsync();
        using var buffer = new MemoryStream();
        await retrievedContent.CopyToAsync(buffer);
        Assert.Equal(content, buffer.ToArray());

        var found = await provider.FindArtifactsAsync(authority);
        Assert.Contains(found, artifact => artifact.Reference.Equals(stored.Reference));

        var authoritativeReference = new AuthoritativeReference(
            scheme,
            "Artifact",
            "current",
            "latest",
            authority,
            "Current");
        await provider.SetAuthoritativeReferenceAsync(authoritativeReference, stored.Reference);

        var resolved = await provider.ResolveAuthoritativeReferenceAsync(authoritativeReference);
        Assert.Equal(stored.Reference, resolved);
    }
}
