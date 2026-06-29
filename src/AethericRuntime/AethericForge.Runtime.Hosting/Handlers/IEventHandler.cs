namespace AethericForge.Runtime.Hosting;

using AethericForge.Runtime.Bus.Abstractions;

public interface IEventHandler<TEvent> where TEvent : notnull
{
    Task Handle(TEvent @event, MessageContext context);
}
