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

    +--------------------+        +--------------------+        +----------------------+
    |  AethericHost      |  pub   |  ITransport        |  route |  Consumers/Handlers  |
    |  (composition)     +------->|  InMemory/RabbitMQ |------->|  (in-process)         |
    +--------------------+        +--------------------+        +----------------------+
              |
              | uses
              v
    +--------------------+
    |  IRepo<T>          |
    |  InMemory/MongoDB  |
    +--------------------+

## C# runtime library

### Projects and responsibilities

-   `AethericForge.Runtime.Bus.Abstractions`
    -   Defines the message bus interfaces (`IBroker`, `ITransport`),
        `Envelope`, and handler delegate.
-   `AethericForge.Runtime.Bus`
    -   Implements `MessageBroker`, which wraps a transport and provides
        routing helpers.
    -   Implements transports: `InMemoryTransport` and
        `RabbitMqTransport`.
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

## Known gaps

-   RabbitMQ provisioning beyond tests is not defined.
-   Production service scaffolding and deployment templates are not included.
