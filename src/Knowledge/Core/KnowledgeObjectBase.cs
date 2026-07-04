using System.Collections.Generic;

namespace Knowledge.Core
{
    public abstract class KnowledgeObjectBase : IKnowledgeObject
    {
        public abstract string Id { get; }
        public abstract Metadata Metadata { get; }
        public abstract IList<Attribute> Attributes { get; }
        public abstract IList<string> Methods { get; }
    }
}
