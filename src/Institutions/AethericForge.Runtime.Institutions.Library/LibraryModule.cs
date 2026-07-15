using AethericForge.Runtime.Institutions.Abstractions.Composition;
using AethericForge.Runtime.Institutions.Abstractions.Models;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Institutions.Library;

public class LibraryModule : InstitutionModule
{
    public override void Configure(IInstitutionBuilder builder)
    {
        builder.SetDescriptor(new InstitutionDescriptor(
            "Library",
            new Version("1.0.0"),
            "A Library institution, representing a repository of knowledge."
        ));
    }
}
