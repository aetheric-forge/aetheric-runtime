using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AethericForge.Runtime.Bus;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Bus.Transports;

namespace AethericForge.Runtime.LogService;

public class Program
{
    public static void Main(string[] args)
    {
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(o =>
                {
                    o.TimestampFormat = "HH:mm:ss ";
                });
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(services =>
            {
                var url = Environment.GetEnvironmentVariable("RABBITMQ_URL")
                          ?? throw new Exception("RABBITMQ_URL is not set");

                services.AddSingleton<ITransport>(_ => new RabbitMqTransport(url, exchangeName: "parallel_you.commands"));
                services.AddSingleton<IBroker, MessageBroker>();

                services.AddHostedService<LogCommandWorker>();
            })
            .Build()
            .Run();
    }
}
