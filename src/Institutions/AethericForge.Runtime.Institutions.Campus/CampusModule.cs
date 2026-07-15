using AethericForge.Runtime.Institutions.Abstractions.Composition;
using AethericForge.Runtime.Institutions.Abstractions.Models;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Institutions.Campus;

public class CampusModule : InstitutionModule
{
    public override void Configure(IInstitutionBuilder builder)
    {
        builder.SetDescriptor(new InstitutionDescriptor(
            "Campus",
            new Version("1.0.0"),
            "A Campus institution, representing the root of an institutional hierarchy."
        ));
    }
}
