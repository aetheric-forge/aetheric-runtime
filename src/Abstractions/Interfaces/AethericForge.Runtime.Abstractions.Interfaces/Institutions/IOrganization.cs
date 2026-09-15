namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions;

/// <summary>
/// Represents an Organization: reusable operational structure embedded within an owning Institution's
/// scope (docs/specs/institution.md §5). Unlike an Institution, an Organization has no constitutional
/// identity or lifecycle of its own and does not participate in ancestor-scope capability resolution -
/// it derives its authority entirely from the Institution that owns it. Many instances of the same
/// Organization shape may be registered under one owner, keyed by id rather than by exclusive contract
/// type, which is what lets an Institution host several of the same kind of Organization at once.
/// </summary>
public interface IOrganization
{
    IOrganizationContext Context { get; }
}
