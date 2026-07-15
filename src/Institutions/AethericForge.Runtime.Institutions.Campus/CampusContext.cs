using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Campus;

public class CampusContext(IInstitutionTemplate template, IServiceProvider services)
    : InstitutionContext(template, services, null), ICampusContext
{
}