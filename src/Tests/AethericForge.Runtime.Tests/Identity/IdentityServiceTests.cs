using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Identity.Primitives;
using AethericForge.Runtime.Providers.Identity.InMemory;
using AethericForge.Runtime.Services.Identity;
using AethericForge.Runtime.Services.Identity.Lifecycle;

namespace AethericForge.Runtime.Tests.Identity;

public class IdentityServiceTests
{
    private readonly IIdentityLifecycleService _lifecycleService = new IdentityLifecycleService(Enumerable.Empty<IIdentityLifecyclePolicy>());

    [Fact]
    public void Constructor_Throws_When_Providers_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new IdentityService(null!, _lifecycleService));
    }

    [Fact]
    public void Constructor_Throws_When_LifecycleService_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new IdentityService(Enumerable.Empty<IIdentityProvider>(), null!));
    }

    [Fact]
    public async Task AuthenticateAsync_Calls_Correct_Provider()
    {
        // Arrange
        var scheme = IdentityScheme.OpenIdConnect;
        var credentials = new Dictionary<string, string> { ["username"] = "user", ["password"] = "pass" };
        var subject = new IdentitySubject("user", scheme);
        
        var provider = new InMemoryIdentityProvider("test", scheme);
        provider.AddSubject(subject, "pass");
        var service = new IdentityService(new[] { provider }, _lifecycleService);

        // Act
        var result = await service.AuthenticateAsync(scheme, credentials);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user", result.Subject.SubjectId);
        Assert.True(result.IsAuthenticated);
    }

    [Fact]
    public async Task AuthenticateAsync_Throws_When_Scheme_Not_Found()
    {
        // Arrange
        var service = new IdentityService(Enumerable.Empty<IIdentityProvider>(), _lifecycleService);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            service.AuthenticateAsync(IdentityScheme.OpenIdConnect, new Dictionary<string, string>()));
    }

    [Fact]
    public async Task ResolveSubjectAsync_Calls_Correct_Provider()
    {
        // Arrange
        var scheme = IdentityScheme.OpenIdConnect;
        var subjectId = "test-user";
        var subject = new IdentitySubject(subjectId, scheme);
        
        var provider = new InMemoryIdentityProvider("test", scheme);
        provider.AddSubject(subject);
        var service = new IdentityService(new[] { provider }, _lifecycleService);

        // Act
        var result = await service.ResolveSubjectAsync(scheme, subjectId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(subjectId, result.SubjectId);
    }

    [Fact]
    public async Task ResolvePrincipalAsync_Returns_Principal_If_Provider_Returns_Principal()
    {
        // Arrange
        var scheme = IdentityScheme.OpenIdConnect;
        var subject = new IdentitySubject("id", scheme);
        
        var provider = new InMemoryIdentityProvider("test", scheme);
        provider.AddSubject(subject, "pass");
        var service = new IdentityService(new[] { provider }, _lifecycleService);

        // Act
        var result = await service.ResolvePrincipalAsync(subject);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("id", result.Subject.SubjectId);
    }

    [Fact]
    public async Task ResolvePrincipalAsync_Authenticates_If_Provider_Returns_Subject()
    {
        // Arrange
        var scheme = IdentityScheme.OpenIdConnect;
        var subjectId = "id";
        var subject = new IdentitySubject(subjectId, scheme);
        
        var provider = new InMemoryIdentityProvider("test", scheme);
        provider.AddSubject(subject, "pass");
        var service = new IdentityService(new[] { provider }, _lifecycleService);

        // Act
        var result = await service.ResolvePrincipalAsync(subject);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(subjectId, result.Subject.SubjectId);
    }

    [Fact]
    public async Task ResolvePrincipalAsync_Returns_Null_If_Provider_Returns_Null()
    {
        // Arrange
        var scheme = IdentityScheme.OpenIdConnect;
        var subject = new IdentitySubject("id", scheme);
        
        var provider = new InMemoryIdentityProvider("test", scheme);
        var service = new IdentityService(new[] { provider }, _lifecycleService);

        // Act
        var result = await service.ResolvePrincipalAsync(subject);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePrincipalAsync_Throws_When_Scheme_Not_Found()
    {
        // Arrange
        var service = new IdentityService(Enumerable.Empty<IIdentityProvider>(), _lifecycleService);
        var subject = new IdentitySubject("id", IdentityScheme.OpenIdConnect);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ResolvePrincipalAsync(subject));
    }

    [Fact]
    public async Task AuthenticateAsync_Uses_Correct_Provider_Among_Multiple()
    {
        // Arrange
        var provider1 = new InMemoryIdentityProvider("p1", IdentityScheme.Local);
        var provider2 = new InMemoryIdentityProvider("p2", IdentityScheme.OpenIdConnect);
        var subject = new IdentitySubject("user", IdentityScheme.OpenIdConnect);
        provider2.AddSubject(subject, "pass");
        
        var service = new IdentityService(new[] { provider1, provider2 }, _lifecycleService);

        // Act
        var result = await service.AuthenticateAsync(IdentityScheme.OpenIdConnect, new Dictionary<string, string> { ["username"] = "user", ["password"] = "pass" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user", result.Subject.SubjectId);
    }

    private class StubIdentityProvider : IIdentityProvider
    {
        private readonly IIdentitySubject? _subject;
        private readonly IPrincipalIdentity? _principal;

        public StubIdentityProvider(IdentityScheme scheme, IIdentitySubject? subject = null, IPrincipalIdentity? principal = null)
        {
            Scheme = scheme;
            _subject = subject;
            _principal = principal;
        }

        public string Name => "Stub";
        public IdentityScheme Scheme { get; }

        public string? LastSubjectId { get; private set; }
        public IReadOnlyDictionary<string, string>? LastCredentials { get; private set; }

        public Task<IIdentitySubject?> ResolveSubjectAsync(string subjectId, CancellationToken cancellationToken = default)
        {
            LastSubjectId = subjectId;
            return Task.FromResult(_subject);
        }

        public Task<IPrincipalIdentity?> AuthenticateAsync(IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken = default)
        {
            LastCredentials = credentials;
            return Task.FromResult(_principal);
        }
    }

    private class StubPrincipalAndSubject : IPrincipalIdentity, IIdentitySubject
    {
        public StubPrincipalAndSubject(string subjectId, IdentityScheme scheme)
        {
            SubjectId = subjectId;
            Scheme = scheme;
            Subject = this;
        }

        public string SubjectId { get; }
        public IdentityScheme Scheme { get; }
        public string? DisplayName => null;
        public IdentityState State => IdentityState.Active;
        public IReadOnlyCollection<IIdentityClaim> Claims => Array.Empty<IIdentityClaim>();
        public IIdentitySubject Subject { get; }
        public bool IsAuthenticated => true;
    }
}
