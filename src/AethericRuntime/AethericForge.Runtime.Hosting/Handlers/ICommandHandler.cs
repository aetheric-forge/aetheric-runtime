namespace AethericForge.Runtime.Hosting;

using AethericForge.Runtime.Bus.Abstractions;

public interface ICommandHandler<TCommand> where TCommand : notnull
{
    Task Handle(TCommand command, MessageContext context);
}
