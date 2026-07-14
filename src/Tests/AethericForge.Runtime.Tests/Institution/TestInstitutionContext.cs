using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace AethericForge.Runtime.Tests.Institution;

internal sealed class TestInstitutionContext : IInstitutionContext
{
    public IInstitutionTemplate Template { get; } = new TestInstitutionTemplate();

    public IServiceProvider Services { get; } = new ServiceCollection()
        .BuildServiceProvider();
}
