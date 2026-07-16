using AethericForge.Runtime.Institutions.Archive;
using AethericForge.Runtime.Institutions.Library;
using AethericForge.Runtime.Institutions.PostOffice;
using AethericForge.Runtime.Institutions.Registry;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Campus;

public sealed class Campus : InstitutionBase, ICampus
{
    public Campus(ICampusContext context)
        : base(ValidateContext(context))
    {
        Context = context;
    }

    public new ICampusContext Context { get; }

    private static ICampusContext ValidateContext(ICampusContext? context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Parent is not null)
        {
            throw new ArgumentException(
                "A campus context cannot have a parent institution.",
                nameof(context));
        }

        return context;
    }

    public IArchive Archive => Resolve<IArchive>();
    public IPostOffice PostOffice => Resolve<IPostOffice>();
    public IRegistry Registry => Resolve<IRegistry>();
    public ILibrary Library => Resolve<ILibrary>();
}