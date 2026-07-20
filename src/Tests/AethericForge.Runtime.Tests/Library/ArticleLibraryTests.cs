using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Models.Library.Articles;
using AethericForge.Runtime.Providers.Knowledge.InMemory;
using AethericForge.Runtime.Services.Knowledge;
using AethericForge.Runtime.Services.Library;
using Xunit;

namespace AethericForge.Runtime.Tests.ArticlePublishing;

public sealed class ArticleLibraryTests
{
    private const string KnowledgeScheme = "TestLibrary";

    private static readonly InstitutionReference LibraryAuthority = new()
    {
        Id = "forge-library",
        Name = "Library",
    };

    private static readonly InstitutionReference CampusOrigin = new()
    {
        Id = "forge-campus",
        Name = "Forge Campus",
    };

    [Fact]
    public async Task CreateAsync_PublishesCanonicalLibraryArticle()
    {
        var service = CreateService();
        var draft = CreateDraft();

        var created = await service.CreateAsync(draft);
        var restored = await service.GetAsync(draft.Identity.CanonicalId);
        var listed = await service.ListAsync();

        Assert.Equal(LibraryAuthority, created.Provenance.Authority);
        Assert.Equal(CampusOrigin, created.Provenance.Origin);
        Assert.Equal(1, created.Publication.Revision);
        Assert.NotNull(restored);
        Assert.Equal(created.Identity, restored.Identity);
        Assert.Single(listed);
        Assert.Equal(created.Identity.CanonicalId, listed.Single().Identity.CanonicalId);
    }

    [Fact]
    public async Task SaveAsync_CreatesRevisionAndRejectsStaleEdit()
    {
        var service = CreateService();
        var created = await service.CreateAsync(CreateDraft());
        var changed = created with
        {
            Identity = created.Identity with { Title = "Changed title" },
        };

        var saved = await service.SaveAsync(changed);

        Assert.Equal(2, saved.Publication.Revision);
        Assert.Equal("Changed title", (await service.GetAsync(
            created.Identity.CanonicalId))?.Identity.Title);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(changed));
        Assert.Contains("stale", exception.Message);
    }

    [Fact]
    public async Task SaveAsync_RejectsNonLibraryAuthority()
    {
        var service = CreateService();
        var created = await service.CreateAsync(CreateDraft());
        var changed = created with
        {
            Provenance = created.Provenance with
            {
                Authority = new InstitutionReference
                {
                    Id = "outside-library",
                    Name = "Outside Library",
                },
            },
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(changed));

        Assert.Contains("authority", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransitionAsync_EnforcesWorkflowAndRecordsPublication()
    {
        var service = CreateService();
        var editor = CreateAgent("Editor");
        var created = await service.CreateAsync(CreateDraft());

        var reviewed = await service.TransitionAsync(
            created.Identity.CanonicalId,
            ArticlePublicationState.InReview,
            editor);
        var approved = await service.TransitionAsync(
            created.Identity.CanonicalId,
            ArticlePublicationState.Approved,
            editor);
        var publishedAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
        var published = await service.TransitionAsync(
            created.Identity.CanonicalId,
            ArticlePublicationState.Published,
            editor,
            publishedAt);

        Assert.Equal(ArticlePublicationState.InReview, reviewed.Publication.State);
        Assert.Equal(ArticlePublicationState.Approved, approved.Publication.State);
        Assert.Equal(ArticlePublicationState.Published, published.Publication.State);
        Assert.Equal(4, published.Publication.Revision);
        Assert.Equal(publishedAt, published.Publication.PublishedAtUtc);
        Assert.Equal(editor, published.Provenance.UpdatedBy);
        Assert.Equal(3, published.Provenance.SourceHistory.Count);
    }

    [Fact]
    public async Task TransitionAsync_RejectsInvalidTransition()
    {
        var service = CreateService();
        var created = await service.CreateAsync(CreateDraft());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TransitionAsync(
                created.Identity.CanonicalId,
                ArticlePublicationState.Published,
                CreateAgent("Editor")));

        Assert.Contains("Draft to Published", exception.Message);
    }

    private static IArticleLibrary CreateService()
    {
        var provider = new InMemoryKnowledgeProvider(KnowledgeScheme);
        IKnowledgeService knowledge = new KnowledgeService(
            [provider],
            new Team<ICuratorClerk>([]));

        return new ArticleLibrary(
            knowledge,
            new ArticleLibraryOptions
            {
                KnowledgeScheme = KnowledgeScheme,
                Authority = LibraryAuthority,
                DefaultOrigin = CampusOrigin,
            });
    }

    private static Article CreateDraft()
    {
        var author = CreateAgent("Author");
        return new Article
        {
            Identity = new ArticleIdentity
            {
                Title = "A Forge Article",
                Slug = "a-forge-article",
            },
            Authorship = new ArticleAuthorship
            {
                Authors = [author],
                Institution = CampusOrigin,
            },
            Content = new ArticleContent
            {
                Body = "# Article",
            },
            Provenance = new ArticleProvenance
            {
                Authority = LibraryAuthority,
                CreatedBy = author,
                UpdatedBy = author,
            },
        };
    }

    private static ArticleAgent CreateAgent(string name)
        => new()
        {
            IdentityReference = $"forge-person:{name.ToLowerInvariant()}",
            DisplayName = name,
        };
}
