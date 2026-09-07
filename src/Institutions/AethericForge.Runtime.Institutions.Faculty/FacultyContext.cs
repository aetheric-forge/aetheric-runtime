using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Faculty;

public sealed class FacultyContext(
    IInstitutionTemplate template,
    IServiceProvider services,
    IInstitution? parent = null)
    : InstitutionContext(template, services, parent), IFacultyContext;
