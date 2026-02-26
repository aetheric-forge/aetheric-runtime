namespace AethericForge.Runtime.Hosting;

public interface ICommandHandler<TCommand> where TCommand : notnull
{
    Task Handle(TCommand command, MessageContext context);
}
