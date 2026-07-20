using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.PostOffice;

public sealed class PostOffice(
    IPostOfficeContext context,
    IPostmaster postmaster)
    : InstitutionBase(context), IPostOffice
{
    public IPostmaster Postmaster { get; } =
        postmaster ?? throw new ArgumentNullException(nameof(postmaster));

    public new IPostOfficeContext Context => (IPostOfficeContext)base.Context;
}
