using AethericForge.Runtime.Abstractions.Interfaces.Decisions.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Decisions;

/// <summary>
/// Sealed implementation of <see cref="IDecisions"/>. A concrete Decisions Office's behavior lives
/// entirely in the <see cref="IRecorder"/> it is handed - this class only carries the Organization
/// context, the same way <c>Archive</c> and <c>Faculty</c> carry Institution context for their own owned
/// Authority.
/// </summary>
public sealed class Decisions(IDecisionsContext context, IRecorder recorder)
    : OrganizationBase(context), IDecisions
{
    public new IDecisionsContext Context { get; } =
        context ?? throw new ArgumentNullException(nameof(context));

    public IRecorder Recorder { get; } =
        recorder ?? throw new ArgumentNullException(nameof(recorder));
}
