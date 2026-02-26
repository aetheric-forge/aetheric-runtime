namespace AethericForge.Runtime.Bus.Abstractions;

public delegate Task EnvelopeHandler(Envelope envelope, CancellationToken ct);

public interface ITransport
{
    Task PublishAsync(Envelope envelope, CancellationToken ct = default);
    Task SubscribeAsync(string pattern, EnvelopeHandler handler, CancellationToken ct = default);
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}

