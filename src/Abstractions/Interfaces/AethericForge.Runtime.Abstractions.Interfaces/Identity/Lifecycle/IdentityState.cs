namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
public enum IdentityState
{
    Unknown = 0,
    Pending,
    Active,
    Suspended,
    Disabled,
    Revoked,
    Expired,
    Retired
}