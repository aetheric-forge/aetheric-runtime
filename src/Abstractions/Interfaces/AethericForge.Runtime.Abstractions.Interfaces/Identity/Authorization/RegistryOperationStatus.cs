namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;

/// <summary>
/// Outcome of a write against the identity provider's admin API
/// (<see cref="AethericForge.Runtime.Abstractions.Interfaces.Identity.Services.IRegistryClerk"/>).
/// Distinct from <c>ExternalDirectoryStatus</c>, which models read/query outcomes - writes have
/// their own failure shapes (a duplicate create, an invalid request body) that reads never see.
/// </summary>
public enum RegistryOperationStatus
{
    Succeeded,
    AlreadyExists,
    NotFound,
    Invalid,
    Unauthorized,
    Unavailable
}
