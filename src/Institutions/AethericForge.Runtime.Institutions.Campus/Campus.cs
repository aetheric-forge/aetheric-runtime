using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
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
}