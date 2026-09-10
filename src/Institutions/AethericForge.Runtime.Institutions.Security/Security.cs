using AethericForge.Runtime.Abstractions.Interfaces.Security.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Security;

public sealed class Security(ISecurityContext context, ISentinel sentinel)
    : InstitutionBase(context), ISecurity
{
    public new ISecurityContext Context => (ISecurityContext)base.Context;

    public ISentinel Sentinel { get; } = sentinel ?? throw new ArgumentNullException(nameof(sentinel));
}
