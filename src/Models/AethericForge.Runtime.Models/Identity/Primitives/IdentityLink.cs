using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Models.Identity.Primitives;

public sealed record IdentityLink(
    IIdentityIdentifier Primary,
    IIdentityIdentifier Linked,
    string LinkType) : IIdentityLink;
