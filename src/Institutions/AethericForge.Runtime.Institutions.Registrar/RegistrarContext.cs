using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Registrar;

public sealed class RegistrarContext(
    IInstitutionTemplate template,
    IServiceProvider services,
    IInstitution? parent = null)
    : InstitutionContext(template, services, parent), IRegistrarContext;
