using AethericForge.Runtime.Abstractions.Interfaces.Decisions.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;

namespace AethericForge.Runtime.Institutions.Decisions;

/// <summary>
/// Represents an Institution that keeps a durable, reviewable record of the decisions made within its
/// owning scope (e.g. an ADR-style decision log). A Decisions Office is not sovereign - it exists to be
/// mounted inside another Institution (a Campus, a Faculty, or eventually an Organization) the way
/// Archive or Library are, rather than standing alone as its own Campus.
/// </summary>
public interface IDecisions : IInstitution
{
    IRecorder Recorder { get; }
}
