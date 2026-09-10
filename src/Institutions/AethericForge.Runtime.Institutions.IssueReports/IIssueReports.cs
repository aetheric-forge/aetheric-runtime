using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Services;

namespace AethericForge.Runtime.Institutions.IssueReports;

/// <summary>
/// Represents an Institution that manages the intake, tracking, and resolution of issue reports.
/// </summary>
public interface IIssueReports : IInstitution
{
    IWarden Warden { get; }
}
