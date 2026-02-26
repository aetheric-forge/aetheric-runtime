namespace AethericForge.Runtime.Hosting;

public interface IEventHandler<TEvent> where TEvent : notnull
{
    Task Handle(TEvent @event, MessageContext context);
}
