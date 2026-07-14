using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Identity.Primitives;
using AethericForge.Runtime.Services.Identity;
using AethericForge.Runtime.Services.Identity.Lifecycle;
using Xunit;

namespace AethericForge.Runtime.Tests;

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
        var credentials = new Dictionary<string, string> { ["u"] = "p" };
        var principal = new PrincipalIdentity(new IdentitySubject("id", scheme), true);
        
        var provider = new StubIdentityProvider(scheme, principal: principal);
        var service = new IdentityService(new[] { provider }, _lifecycleService);

        // Act
        var result = await service.AuthenticateAsync(scheme, credentials);

        // Assert
        Assert.Same(principal, result);
        Assert.Equal(credentials, provider.LastCredentials);
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
        
        var provider = new StubIdentityProvider(scheme, subject: subject);
        var service = new IdentityService(new[] { provider }, _lifecycleService);

        // Act
        var result = await service.ResolveSubjectAsync(scheme, subjectId);

        // Assert
        Assert.Same(subject, result);
        Assert.Equal(subjectId, provider.LastSubjectId);
    }

    [Fact]
    public async Task ResolvePrincipalAsync_Returns_Principal_If_Provider_Returns_Principal()
    {
        // Arrange
        var scheme = IdentityScheme.OpenIdConnect;
        var subject = new IdentitySubject("id", scheme);
        var principal = new StubPrincipalAndSubject("id", scheme);
        
        var provider = new StubIdentityProvider(scheme, subject: principal);
        var service = new IdentityService(new[] { provider }, _lifecycleService);

        // Act
        var result = await service.ResolvePrincipalAsync(subject);

        // Assert
        Assert.Same(principal, result);
    }

    [Fact]
    public async Task ResolvePrincipalAsync_Authenticates_If_Provider_Returns_Subject()
    {
        // Arrange
        var scheme = IdentityScheme.OpenIdConnect;
        var subjectId = "id";
        var subject = new IdentitySubject(subjectId, scheme);
        var resolvedSubject = new IdentitySubject(subjectId, scheme);
        var principal = new PrincipalIdentity(resolvedSubject, true);
        
        var provider = new StubIdentityProvider(scheme, subject: resolvedSubject, principal: principal);
        var service = new IdentityService(new[] { provider }, _lifecycleService);

        // Act
        var result = await service.ResolvePrincipalAsync(subject);

        // Assert
        Assert.Same(principal, result);
        Assert.NotNull(provider.LastCredentials);
        Assert.Equal(subjectId, provider.LastCredentials["subjectId"]);
    }

    [Fact]
    public async Task ResolvePrincipalAsync_Returns_Null_If_Provider_Returns_Null()
    {
        // Arrange
        var scheme = IdentityScheme.OpenIdConnect;
        var subject = new IdentitySubject("id", scheme);
        
        var provider = new StubIdentityProvider(scheme);
        var service = new IdentityService(new[] { provider }, _lifecycleService);

        // Act
        var result = await service.ResolvePrincipalAsync(subject);

        // Assert
        Assert.Null(result);
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
