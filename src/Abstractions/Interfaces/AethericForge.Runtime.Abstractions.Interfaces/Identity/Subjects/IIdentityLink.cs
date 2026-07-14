namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

public interface IIdentityLink
{
    IIdentityIdentifier Primary { get; }
    IIdentityIdentifier Linked { get; }
    string LinkType { get; }
}