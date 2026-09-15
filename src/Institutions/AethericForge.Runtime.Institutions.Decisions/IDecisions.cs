using AethericForge.Runtime.Abstractions.Interfaces.Decisions.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;

namespace AethericForge.Runtime.Institutions.Decisions;

/// <summary>
/// Represents an Organization that keeps a durable, reviewable record of the decisions made within its
/// owning Institution (e.g. an ADR-style decision log). A Decisions Office is not sovereign - it derives
/// its authority entirely from whatever Institution owns it, rather than standing alone as its own
/// Campus, which is why this is an IOrganization rather than an IInstitution.
/// </summary>
public interface IDecisions : IOrganization
{
    IRecorder Recorder { get; }
}
