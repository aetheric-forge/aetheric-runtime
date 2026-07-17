using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Identity.Primitives;
using AethericForge.Runtime.Providers.Identity.InMemory;
using AethericForge.Runtime.Services.Identity;
using AethericForge.Runtime.Services.Identity.Lifecycle;

namespace AethericForge.Runtime.Tests.Identity;

public class RegistrarTests
{
    private readonly IIdentityLifecycleService _lifecycleService = new IdentityLifecycleService(Enumerable.Empty<IIdentityLifecyclePolicy>());

    [Fact]
    public async Task AuthenticateAsync_Delegates_To_Service()
    {
        // Arrange
        var scheme = IdentityScheme.OpenIdConnect;
        var credentials = new Dictionary<string, string> { ["username"] = "user", ["password"] = "pass" };
        var subject = new IdentitySubject("user", scheme);
        
        var provider = new InMemoryIdentityProvider("test", scheme);
        provider.AddSubject(subject, "pass");
        var service = new IdentityService(new[] { provider }, _lifecycleService);
        var registrar = new Registrar(service);

        // Act
        var result = await registrar.AuthenticateAsync(scheme, credentials);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user", result.Subject.SubjectId);
    }
}
