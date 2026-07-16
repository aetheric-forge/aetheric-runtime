using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Identity.Primitives;
using AethericForge.Runtime.Providers.Identity.InMemory;
using Xunit;

namespace AethericForge.Runtime.Tests.Identity;

public class InMemoryIdentityProviderTests
{
    private readonly InMemoryIdentityProvider _provider = new(Name, Scheme);
    private const string Name = "test-provider";
    private const IdentityScheme Scheme = IdentityScheme.Local;

    [Fact]
    public async Task ResolveSubject_WorksCorrectly()
    {
        // Arrange
        var subjectId = "user1";
        var subject = new IdentitySubject(subjectId, Scheme);
        _provider.AddSubject(subject);

        // Act
        var resolved = await _provider.ResolveSubjectAsync(subjectId);

        // Assert
        Assert.NotNull(resolved);
        Assert.Equal(subjectId, resolved.SubjectId);
    }

    [Fact]
    public async Task Authenticate_WorksCorrectly()
    {
        // Arrange
        var username = "user1";
        var password = "password123";
        var subject = new IdentitySubject(username, Scheme);
        _provider.AddSubject(subject, password);

        var credentials = new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password
        };

        // Act
        var principal = await _provider.AuthenticateAsync(credentials);

        // Assert
        Assert.NotNull(principal);
        Assert.True(principal.IsAuthenticated);
        Assert.Equal(username, principal.Subject.SubjectId);
    }

    [Fact]
    public async Task Authenticate_ReturnsNull_OnWrongPassword()
    {
        // Arrange
        var username = "user1";
        var subject = new IdentitySubject(username, Scheme);
        _provider.AddSubject(subject, "correct-password");

        var credentials = new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = "wrong-password"
        };

        // Act
        var principal = await _provider.AuthenticateAsync(credentials);

        // Assert
        Assert.Null(principal);
    }
}
