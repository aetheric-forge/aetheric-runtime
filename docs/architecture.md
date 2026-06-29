# Architecture

This document describes the architecture of the Aetheric Runtime
codebase as it exists in this repository. It is reconstructed from the
current code and tests.

## Scope and intent

The repository contains a C# runtime library that defines an
envelope-based message bus, a minimal application host, and a repository
abstraction with backends.

These components are designed to work with external services (RabbitMQ,
MongoDB). The runtime includes a minimal host/composition layer, but no
production service scaffolding or external API gateway is included.

## High-level architecture

    +--------------------+        +------------------------------+        +----------------------+
    |  AethericHost      |  pub   |  ITransport                  |  route |  Consumers/Handlers  |
    |  (composition)     +------->|  InMemory/RabbitMQ           +------->|  (in-process)         |
    +--------------------+        +------------------------------+        +----------------------+
              |
              | uses
              v
    +--------------------+
    |  IRepo<T>          |
    |  InMemory/MongoDB  |
    +--------------------+

    # (Planned) SharedMemory (Redis) transport/repo to be added in 1.0+

## C# runtime library

### Projects and responsibilities

-   `AethericForge.Runtime.Bus.Abstractions`
    -   Defines the message bus interfaces (`IBroker`, `ITransport`),
        `Envelope`, and handler delegate.
-   `AethericForge.Runtime.Bus`
    -   Implements `MessageBroker`, which wraps a transport and provides
        routing helpers.
    -   Implements transports: `InMemoryTransport`,
        `RabbitMqTransport`.
    -   _(Future: SharedMemory/Redis transport on the roadmap)_
-   `AethericForge.Runtime.Hosting`
    -   Implements `AethericHost` and `AethericHostBuilder`, plus
        handler interfaces and a message context.
-   `AethericForge.Runtime.Repo.Abstractions`
    -   Defines the repository interface (`IRepo<T>`) and filter spec
        (`IFilterSpec`, `FilterSpec`).
-   `AethericForge.Runtime.Repo`
    -   Implements repository backends: `InMemoryRepo` and `MongoRepo`.
-   `AethericForge.Runtime.Util`
    -   Shared utility functions (slug generation, dictionary helpers).
-   `AethericForge.Runtime.Tests`
    -   Contract tests for bus and repo semantics, including optional
        integration cases when environment variables are set.

### Envelope model and routing keys

Messages are carried in envelopes (`Envelope` / `Envelope<T>`). Each
envelope includes a `Kind` and routing metadata.

Envelope kinds and required fields:

-   `Request`: requires `Service` and `Verb`
-   `Event`: requires `Topic`
-   `Response` / `Error`: require `CorrelationId`

Routing keys are derived from the envelope fields:

-   Request: `{service}.{verb}`
-   Event: `{topic}`
-   Response/Error: `reply.{client_id}` (from `Meta["client_id"]`)

### Transport contract

The runtime assumes a shared transport lifecycle contract:

-   `SubscribeAsync` may be called before `StartAsync`.
-   Transports should queue/apply pre-start subscriptions once started.
-   `PublishAsync` before start should fail with
    `InvalidOperationException("Transport not started")`.

This keeps `MessageBroker.Route(...)` and host builder route registration
transport-agnostic.

### Runtime lifecycle and hosting

The runtime provides a minimal host/composition layer.

`AethericHost` responsibilities:

-   Start and stop transports (`StartAsync`, `StopAsync`)
-   Expose the broker (`IBroker`) and registered repositories
-   Run until cancellation (`RunAsync`)

`AethericHostBuilder` responsibilities:

-   Configure transports (`UseTransport`, `AddTransport`)
-   Register repositories (`UseRepo`)
-   Register handlers:
    -   `AddCommandHandler<T>(Func<T, MessageContext, Task>)`
    -   `AddEventHandler<T>(Func<T, MessageContext, Task>)`
    -   `AddHandler<T>(pattern, handler)` (explicit envelope routing)
    -   `AddHandlersFromNamespace(...)`

### Test architecture

Transport contract tests run via a shared matrix:

-   Always include `InMemoryTransport`.
-   Include `RabbitMqTransport` when `RABBITMQ_URL` is set.
-   (Planned) Redis-backed "SharedMemory" transport in future test matrix runs where configuration is provided.

Host lifecycle coverage validates that routes registered during
`BuildAsync` are honored after `StartAsync` across transport types.

## Known gaps & roadmap

-   RabbitMQ provisioning beyond tests is not defined.
-   MongoDB repository backend implemented, test coverage finalizing for 1.0.
-   Redis-backed SharedMemory transport/repo is the next major addition.
-   Production service scaffolding and deployment templates are not included.
-   (Legacy Unix socket transport was dropped from codebase and docs.)
