using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Models.Identity.Primitives;

public sealed record IdentityIdentifier(string SubjectId, IdentityScheme Scheme) : IIdentityIdentifier;
