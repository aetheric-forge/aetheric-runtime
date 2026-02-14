using AethericForge.Runtime.Model.Threads;

namespace AethericForge.Runtime.Model.Messages.Threads;

public sealed class DomainUpdated(Domain domain) : Message<Domain>(domain) { }
