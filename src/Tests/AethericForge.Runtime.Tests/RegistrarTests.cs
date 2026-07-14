using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Institutions.Registrar;
using AethericForge.Runtime.Models.Identity.Primitives;

namespace AethericForge.Runtime.Tests;

public class RegistrarTests
{
    [Fact]
    public async Task RegisterAsync_Registers_And_Identifies_Subject()
    {
        var subject = CreateSubject("student-1");
        var registrar = new Registrar(new StubAuthenticationService());

        var principal = await registrar.RegisterAsync(subject);

        Assert.False(principal.IsAuthenticated);
        Assert.Same(subject, principal.Subject);
        Assert.True(await registrar.ExistsAsync(subject));
        Assert.Same(principal, await registrar.IdentifyAsync(subject));
    }

    [Fact]
    public async Task RegisterAsync_Is_Idempotent_For_Same_Subject()
    {
        var subject = CreateSubject("student-1");
        var registrar = new Registrar(new StubAuthenticationService());

        var first = await registrar.RegisterAsync(subject);
        var second = await registrar.RegisterAsync(new IdentitySubject(
            subject.SubjectId,
            subject.Scheme,
            displayName: "Changed Display Name",
            claims: subject.Claims));

        Assert.Same(first, second);
    }

    [Fact]
    public async Task RegisterAsync_Throws_For_Conflicting_Subject()
    {
        var registrar = new Registrar(new StubAuthenticationService());

        await registrar.RegisterAsync(CreateSubject("student-1"));

        var conflict = new IdentitySubject(
            "student-1",
            IdentityScheme.Local,
            claims: [new IdentityClaim("campus", "north")]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => registrar.RegisterAsync(conflict));
    }

    [Fact]
    public async Task AuthenticateAsync_Returns_Authenticated_Registered_Principal()
    {
        var subject = CreateSubject("student-1");
        var authenticationService = new StubAuthenticationService(new PrincipalIdentity(subject, isAuthenticated: true));
        var registrar = new Registrar(authenticationService);

        await registrar.RegisterAsync(subject);

        var principal = await registrar.AuthenticateAsync(new IdentityCredentials(
            IdentityScheme.Local,
            new Dictionary<string, string> { ["password"] = "correct" }));

        Assert.NotNull(principal);
        Assert.True(principal.IsAuthenticated);
        Assert.Same(subject, principal.Subject);
    }

    [Fact]
    public async Task AuthenticateAsync_Returns_Null_When_Authenticated_Subject_Is_Not_Registered()
    {
        var subject = CreateSubject("student-1");
        var authenticationService = new StubAuthenticationService(new PrincipalIdentity(subject, isAuthenticated: true));
        var registrar = new Registrar(authenticationService);

        var principal = await registrar.AuthenticateAsync(new IdentityCredentials(
            IdentityScheme.Local,
            new Dictionary<string, string> { ["password"] = "correct" }));

        Assert.Null(principal);
    }

    private static IdentitySubject CreateSubject(string subjectId)
    {
        return new IdentitySubject(
            subjectId,
            IdentityScheme.Local,
            displayName: "Student One",
            claims: [new IdentityClaim("campus", "central")]);
    }

    private sealed class StubAuthenticationService : IAuthenticationService
    {
        private readonly IPrincipalIdentity? _principal;

        public StubAuthenticationService(IPrincipalIdentity? principal = null)
        {
            _principal = principal;
        }

        public Task<IPrincipalIdentity?> AuthenticateAsync(
            IdentityScheme scheme,
            IReadOnlyDictionary<string, string> credentials,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_principal);
        }
    }
}
