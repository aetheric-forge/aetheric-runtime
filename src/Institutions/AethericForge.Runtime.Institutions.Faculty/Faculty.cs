using AethericForge.Runtime.Abstractions.Interfaces.Faculty.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Faculty;

public sealed class Faculty(IFacultyContext context, IDean dean)
    : InstitutionBase(context), IFaculty
{
    public new IFacultyContext Context => (IFacultyContext)base.Context;

    public IDean Dean { get; } = dean ?? throw new ArgumentNullException(nameof(dean));
}
