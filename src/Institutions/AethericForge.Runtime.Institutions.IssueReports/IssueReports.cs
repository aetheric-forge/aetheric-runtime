using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.IssueReports;

public sealed class IssueReports(IIssueReportsContext context, IWarden warden)
    : InstitutionBase(context), IIssueReports
{
    public new IIssueReportsContext Context => (IIssueReportsContext)base.Context;

    public IWarden Warden { get; } = warden ?? throw new ArgumentNullException(nameof(warden));
}
