using System.Text.Json;
using AethericForge.Runtime.Models.Library.Articles;
using Xunit;

namespace AethericForge.Runtime.Tests.ArticlePublishing;

public sealed class ArticleModelTests
{
    [Fact]
    public void ForgeArticle_RecordsLibraryAuthorityAndCampusOrigin()
    {
        var author = new ArticleAgent
        {
            IdentityReference = "forge-person:ada",
            DisplayName = "Ada Lovelace",
        };
        var library = new InstitutionReference
        {
            Id = "forge-library",
            Name = "Library",
        };
        var campus = new InstitutionReference
        {
            Id = "forge-campus",
            Name = "Forge Campus",
        };

        var article = new Article
        {
            Identity = new ArticleIdentity
            {
                Title = "Notes from the Forge",
                Slug = "notes-from-the-forge",
            },
            Authorship = new ArticleAuthorship
            {
                Authors = [author],
                Institution = campus,
            },
            Content = new ArticleContent
            {
                Body = "# Notes",
            },
            Provenance = new ArticleProvenance
            {
                Authority = library,
                Origin = campus,
                CreatedBy = author,
                UpdatedBy = author,
            },
        };

        Assert.Equal("forge-library", article.Provenance.Authority.Id);
        Assert.Equal("forge-campus", article.Provenance.Origin?.Id);
        Assert.Equal(ArticlePublicationState.Draft, article.Publication.State);
        Assert.Equal(ArticleVisibility.Private, article.Publication.Visibility);
        Assert.Equal(ArticleContentFormats.Markdown, article.Content.BodyFormat);
        Assert.NotEqual(Guid.Empty, article.Identity.CanonicalId);
    }

    [Fact]
    public void Article_RoundTripsThroughJson()
    {
        var editor = new ArticleAgent { DisplayName = "Forge Editor" };
        var article = new Article
        {
            Identity = new ArticleIdentity
            {
                Title = "A Library Article",
                Slug = "a-library-article",
                Subtitle = "An authoritative record",
            },
            Classification = new ArticleClassification
            {
                Topics = ["Institutions"],
                Tags = ["library", "forge"],
                Series = "Campus Notes",
            },
            Content = new ArticleContent
            {
                Abstract = "A model of institutional publishing.",
                Body = "Article body.",
                Notes =
                [
                    new ArticleNote
                    {
                        Text = "Confirm the subtitle.",
                        CreatedBy = "forge-editor",
                    },
                ],
                References =
                [
                    new ArticleReference
                    {
                        Citation = "The Aetheric Forge",
                        Uri = new Uri("https://example.test/forge"),
                    },
                ],
            },
            Provenance = new ArticleProvenance
            {
                Authority = new InstitutionReference
                {
                    Id = "forge-library",
                    Name = "Library",
                },
                CreatedBy = editor,
                UpdatedBy = editor,
            },
        };

        var json = JsonSerializer.Serialize(article);
        var restored = JsonSerializer.Deserialize<Article>(json);

        Assert.NotNull(restored);
        Assert.Equal(article.Identity, restored.Identity);
        Assert.Equal(article.Publication, restored.Publication);
        Assert.Equal(article.Classification.Series, restored.Classification.Series);
        Assert.Equal(article.Content.Abstract, restored.Content.Abstract);
        Assert.Equal(article.Provenance.Authority, restored.Provenance.Authority);
        Assert.Equal(article.Classification.Tags, restored.Classification.Tags);
        Assert.Equal(article.Content.Notes, restored.Content.Notes);
        Assert.Equal(article.Content.References, restored.Content.References);
    }
}
