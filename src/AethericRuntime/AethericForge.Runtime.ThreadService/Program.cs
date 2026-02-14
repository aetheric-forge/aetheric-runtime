using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AethericForge.Runtime.Bus;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Bus.Transports;
using AethericForge.Runtime.ThreadService;

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

        // Use Bus transport/broker instead of direct RabbitMQ usage
        services.AddSingleton<ITransport>(_ => new RabbitMqTransport(url, exchangeName: "parallel_you.commands"));
        services.AddSingleton<IBroker, MessageBroker>();

        services.AddHostedService<ThreadCommandWorker>();
    })
    .Build()
    .Run();
