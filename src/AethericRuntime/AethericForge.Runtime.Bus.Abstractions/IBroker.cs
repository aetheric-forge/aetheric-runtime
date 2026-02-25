namespace AethericForge.Runtime.Bus.Abstractions;
/**/
public interface IBroker
{
    Task PublishAsync(Envelope envelope, CancellationToken ct = default);
    void Route(string routingKey, EnvelopeHandler handler);
    ITransport Transport { get; }
}
