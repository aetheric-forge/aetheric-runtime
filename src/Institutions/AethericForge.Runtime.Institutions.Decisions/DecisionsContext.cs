using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Decisions;

public sealed class DecisionsContext(
    IInstitutionTemplate template,
    IServiceProvider services,
    IInstitution owner)
    : OrganizationContext(template, services, owner), IDecisionsContext;
