using AethericForge.Runtime.Model.Threads;

namespace AethericForge.Runtime.Model.Messages.Threads;

public sealed class SagaCreated(Saga saga) : Message<Saga>(saga) { }
