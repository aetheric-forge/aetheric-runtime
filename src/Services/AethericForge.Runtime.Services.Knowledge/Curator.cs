using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;

namespace AethericForge.Runtime.Services.Knowledge;

public sealed class Curator(IKnowledgeService knowledgeService, ITeam<ICuratorClerk> team)
    : ICurator
{
    private readonly IKnowledgeService _knowledgeService = knowledgeService;

    public ITeam<ICuratorClerk> Team { get; } = team ?? throw new ArgumentNullException(nameof(team));
}
