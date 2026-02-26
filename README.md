# Aetheric Runtime

**Aetheric Runtime** is an event-driven runtime library for building
envelope-based services with explicit messaging and repository
abstractions.

It provides:

-   An envelope-based message bus (`IBroker`, `ITransport`, `Envelope`)
-   Pluggable transports (InMemory, RabbitMQ, Unix domain sockets)
-   Pluggable repositories (InMemory, MongoDB)
-   A minimal application host (`AethericHost`, `AethericHostBuilder`)
-   Contract-driven tests for routing and persistence semantics

This repository contains runtime building blocks, including a minimal
application host. It does **not** include any external service or API
gateway.

------------------------------------------------------------------------

## Design Goals

v1.0.0 focuses on architectural clarity and development simplicity.

The runtime is built around a few core principles:

-   **Message-shaped reality** -- All cross-boundary interactions occur
    via messages.
-   **Intentional minimalism** -- Abstractions are thin and explicit.
-   **Replaceable infrastructure** -- Transports and repositories can be
    swapped without disturbing the domain.
-   **Deterministic tests** -- InMemory implementations enable
    predictable contract verification.

This project is suitable as a foundation for event-driven applications
that need clear domain modeling without premature microservice
complexity.

------------------------------------------------------------------------

## Repository Structure

### C# Runtime Library

The C# solution defines:

-   `AethericForge.Runtime.Bus.Abstractions`
    -   `IBroker`, `ITransport`, `Envelope`, and handler contracts.
-   `AethericForge.Runtime.Bus`
    -   `MessageBroker`
    -   `InMemoryTransport`
    -   `RabbitMqTransport`
    -   `UnixSocketTransport`
-   `AethericForge.Runtime.Hosting`
    -   `AethericHost`, `AethericHostBuilder`, handler interfaces
-   `AethericForge.Runtime.Repo.Abstractions`
    -   `IRepo<T>`, `IFilterSpec`, and `FilterSpec`
-   `AethericForge.Runtime.Repo`
    -   `InMemoryRepo`
    -   `MongoRepo`
-   `AethericForge.Runtime.Tests`
    -   Contract tests for bus and repository behavior.

------------------------------------------------------------------------

## Core Concepts

### Envelopes

Messages are carried in `Envelope` objects. The envelope includes routing
metadata plus a typed payload (`Envelope<T>`).

Envelope kinds:

-   `Request`: requires `Service` and `Verb`
-   `Event`: requires `Topic`
-   `Response` / `Error`: require `CorrelationId`

Routing keys are derived from the envelope:

-   Request: `{service}.{verb}`
-   Event: `{topic}`
-   Response/Error: `reply.{client_id}` (from `Meta["client_id"]`)

Routing uses dot-delimited topic semantics with `*` and `#` wildcards.

------------------------------------------------------------------------

### Bus Abstraction

-   `IBroker` routes messages by routing key.
-   `ITransport` handles delivery mechanics.

`MessageBroker` wraps a transport and provides:

-   `PublishAsync(Envelope)`
-   `Route(pattern, handler)`

Transport contract notes:

-   `SubscribeAsync` is allowed before `StartAsync`; transports are
    expected to queue pre-start subscriptions and apply them after start.
-   `PublishAsync` before `StartAsync` should throw
    `InvalidOperationException("Transport not started")`.

Transports included:

-   `InMemoryTransport` -- deterministic, ideal for tests.
-   `RabbitMqTransport` -- topic exchange integration.
-   `UnixSocketTransport` -- local IPC over Unix domain sockets.

------------------------------------------------------------------------

### Repository Abstraction

`IRepo` defines:

-   `List`
-   `Get`
-   `Upsert`
-   `Delete`
-   `Clear`

Backends:

-   `InMemoryRepo`
-   `MongoRepo`

Filtering currently supports `Id` via `IFilterSpec`.

------------------------------------------------------------------------

## Minimal Host Example

```csharp
using AethericForge.Runtime.Bus.Transports;
using AethericForge.Runtime.Hosting;
using AethericForge.Runtime.Repo;
using AethericForge.Runtime.Repo.Abstractions;

public record Ping(Guid Id, string Message) : IEntity;

static async Task Main()
{
    var host = await AethericHost.Create("example")
        .UseTransport(new InMemoryTransport())
        .UseRepo<IRepo<Ping>>(new InMemoryRepo<Ping>())
        .AddCommandHandler<Ping>(async (cmd, ctx) =>
        {
            var repo = ctx.GetRepo<IRepo<Ping>>();
            await repo.UpsertAsync(cmd, ctx.CancellationToken);
        })
        .BuildAsync();

    await host.StartAsync();

    // Publish a request envelope via the broker (service + verb routing).
    await host.Broker.PublishAsync(new AethericForge.Runtime.Bus.Abstractions.Envelope<Ping>(
        "example.Ping", new Ping(Guid.NewGuid(), "hello"))
    {
        Kind = AethericForge.Runtime.Bus.Abstractions.EnvelopeKind.Request,
        Service = "example",
        Verb = nameof(Ping)
    });

    await host.StopAsync();
}
```

------------------------------------------------------------------------

## Environment Configuration

Optional test integrations are enabled via environment variables:

    RABBITMQ_URL
    MONGO_URI

------------------------------------------------------------------------

## Known Gaps

This repository intentionally does not include:

-   Exchange/queue provisioning beyond tests
-   Production-focused host/service scaffolding

It provides runtime primitives, not a finished system.

------------------------------------------------------------------------

## Status

v1.0.1 adds:

-   Unix socket transport coverage in the shared bus contract suite
-   Pre-start subscription contract alignment across transports
-   Host lifecycle routing coverage across multiple transport types
