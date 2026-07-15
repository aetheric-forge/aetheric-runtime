using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Institutions.Abstractions.Composition;

public abstract class InstitutionModule : IInstitutionModule
{
    public abstract void Configure(IInstitutionBuilder builder);
}