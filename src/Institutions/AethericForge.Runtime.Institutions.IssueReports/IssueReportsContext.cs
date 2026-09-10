using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.IssueReports;

public sealed class IssueReportsContext(
    IInstitutionTemplate template,
    IServiceProvider services,
    IInstitution? parent = null)
    : InstitutionContext(template, services, parent), IIssueReportsContext;
