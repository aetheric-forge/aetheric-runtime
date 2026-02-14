using AethericForge.Runtime.Model.Messages;

namespace AethericForge.Runtime.Bus.Abstractions;

public delegate Task MessageHandler(Message msg);

public interface ITransport
{
    Task Publish(Message msg);
    Task Subscribe(string pattern, MessageHandler handler);
    Task Start();
    Task Stop();
}