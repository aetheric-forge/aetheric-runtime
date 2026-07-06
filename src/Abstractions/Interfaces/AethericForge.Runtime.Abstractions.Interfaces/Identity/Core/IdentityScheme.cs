namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Core;

public enum IdentityScheme
{
    Anonymous = 0,
    Local = 1,
    ApiKey = 2,
    BearerToken = 3,
    OAuth2 = 4,
    OpenIdConnect = 5,
    Certificate = 6,
    Service = 7
}
