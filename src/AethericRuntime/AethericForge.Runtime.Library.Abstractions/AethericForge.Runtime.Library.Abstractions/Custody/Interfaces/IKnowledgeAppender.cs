namespace AethericForge.Runtime.Library.Abstractions.Custody.Interfaces;

using AethericForge.Runtime.Knowledge.Core.Abstractions.Interfaces;
using AethericForge.Runtime.Knowledge.Core.Abstractions.Models;

public interface IKnowledgeAppender
{
    ValueTask<KnowledgeReference> AppendAsync(
        IKnowledgeObject knowledge,
        CancellationToken cancellationToken = default);
}