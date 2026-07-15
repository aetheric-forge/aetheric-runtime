using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Models.Institutions;
using Microsoft.Extensions.DependencyInjection;

namespace AethericForge.Runtime.Institutions.Library;

public class LibraryContext(
    IInstitutionTemplate template,
    IServiceProvider services,
    IInstitution? parent = null)
    : InstitutionContext(template, services, parent), ILibraryContext
{
    public IKnowledgeService Knowledge => Services.GetRequiredService<IKnowledgeService>();
}
