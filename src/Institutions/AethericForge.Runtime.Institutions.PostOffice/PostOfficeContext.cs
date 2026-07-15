using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.PostOffice;

public sealed class PostOfficeContext(
    IInstitutionTemplate template,
    IServiceProvider services,
    IInstitution parent)
    : InstitutionContext(template, services, parent), IPostOfficeContext
{
}