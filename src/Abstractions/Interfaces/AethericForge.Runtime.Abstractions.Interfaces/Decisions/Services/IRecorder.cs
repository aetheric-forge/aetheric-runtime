using AethericForge.Runtime.Abstractions.Interfaces.Authorities;

namespace AethericForge.Runtime.Abstractions.Interfaces.Decisions.Services;

/// <summary>
/// Leads a Decisions Office: drafts, proposals, review, and supersession of the records it keeps. The
/// operations a Recorder performs are intentionally not specified here yet - they belong to whichever
/// concrete Decisions implementation (e.g. ADR Campus) is mounted as the owning Institution's Authority,
/// the same way a Faculty's Dean or an Archive's Archivist carries its own operational surface.
/// </summary>
public interface IRecorder : IAuthority<IDecisionsClerk>
{

}
