using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Keycloak;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class KeycloakRealmParentCapabilityResolverTests
{
    private static ParentContext Parent => new("https://example.test/fixture", "0000000000000000000000000000000000000000");

    [KeycloakFact]
    public async Task Unrecognized_contracts_are_always_unavailable()
    {
        using var resolver = new KeycloakRealmParentCapabilityResolver(KeycloakFactAttribute.RootCredential(), "master");
        Assert.False(await resolver.IsAvailableAsync(Parent, "ISomethingElse", "university.registry", default));
    }

    [KeycloakFact]
    public async Task An_existing_realm_is_available()
    {
        // "master" always exists on any Keycloak server, including the test one this points at.
        using var resolver = new KeycloakRealmParentCapabilityResolver(KeycloakFactAttribute.RootCredential(), "master");
        Assert.True(await resolver.IsAvailableAsync(Parent, "IRegistrar", "university.registry", default));
    }

    [KeycloakFact]
    public async Task A_realm_that_was_never_created_is_unavailable()
    {
        using var resolver = new KeycloakRealmParentCapabilityResolver(KeycloakFactAttribute.RootCredential(),
            "nonexistent-" + Guid.NewGuid().ToString("N"));
        Assert.False(await resolver.IsAvailableAsync(Parent, "IRegistrar", "university.registry", default));
    }
}
