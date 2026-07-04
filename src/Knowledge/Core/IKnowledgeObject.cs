using System.Collections.Generic;

namespace Knowledge.Core
{
    public interface IKnowledgeObject
    {
        string Id { get; }
        Metadata Metadata { get; }
        IList<Attribute> Attributes { get; }
        IList<string> Methods { get; }
    }
}
