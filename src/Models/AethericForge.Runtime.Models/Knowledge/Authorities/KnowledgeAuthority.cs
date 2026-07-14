using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;

namespace AethericForge.Runtime.Models.Knowledge.Authorities;

public sealed record KnowledgeAuthority(IIdentitySubject Identity, string Context) : IKnowledgeAuthority;
