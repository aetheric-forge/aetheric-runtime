# Architecture

This document describes the architecture of the Aetheric Runtime
codebase as it exists in this repository. It is reconstructed from the
current code and tests.

## Scope and intent

The repository contains two distinct parts:

-   A C# runtime library that defines the core domain model ("threads"),
    a message bus abstraction with transports, and a repository
    abstraction with backends.
-   A Python FastAPI service (`aetheric-runtime-api`) that exposes
    HTTP/WebSocket endpoints and bridges to RabbitMQ exchanges.

These components are designed to work with external services (RabbitMQ,
MongoDB). The C# runtime does not currently include a host/service layer
in this repository; it provides building blocks and tests.

## High-level architecture

    +------------------+          +---------------------+          +----------------------+
    |  HTTP Clients    |  POST    |  aetheric-runtime-  |  publish |  RabbitMQ            |
    |  (commands)      +--------->|  api (FastAPI)      +--------->|  parallel_you.commands|
    +------------------+          +---------------------+          +----------------------+
                                                                       |
                                                                       | (handlers live outside this repo)
                                                                       v
                                                                +------------------+
                                                                |  Runtime services|
                                                                |  (C# libs used)  |
                                                                +------------------+
                                                                       |
                                                                       | publish events
                                                                       v
    +------------------+          +---------------------+          +----------------------+
    |  Web clients     |  WS      |  aetheric-runtime-  |  bind    |  RabbitMQ            |
    |  (session.*)     +<---------|  api (FastAPI)      +<---------|  parallel_you.events |
    +------------------+          +---------------------+          +----------------------+

## C# runtime library

### Projects and responsibilities

-   `AethericForge.Runtime.Model`
    -   Defines the core domain types (threads) and message/event base
        classes.
-   `AethericForge.Runtime.Bus.Abstractions`
    -   Defines the message bus interfaces (`IBroker`, `ITransport`) and
        handler delegate.
-   `AethericForge.Runtime.Bus`
    -   Implements `MessageBroker`, which wraps a transport and provides
        routing helpers.
    -   Implements transports: `InMemoryTransport` and
        `RabbitMqTransport`.
-   `AethericForge.Runtime.Repo.Abstractions`
    -   Defines the repository interface (`IRepo`) and filter spec
        (`FilterSpec`).
-   `AethericForge.Runtime.Repo`
    -   Implements repository backends: `InMemoryRepo` and `MongoRepo`.
-   `AethericForge.Runtime.Util`
    -   Shared utility functions (slug generation, dictionary helpers).
-   `AethericForge.Runtime.Tests`
    -   Contract tests for bus and repo semantics, including optional
        integration cases when environment variables are set.

### Domain model (Threads)

The core entity hierarchy is `Thread`, with concrete types:

-   `Domain`: top-level grouping, optional `Description`.
-   `Saga`: belongs to a `Domain` via `DomainId`.
-   `Story`: belongs to a `Saga` via `SagaId`, includes `Energy` and
    `StoryState`.

All thread types include:

-   `Id` (auto-generated from the title via a slug if omitted)
-   `Title`, `Priority`, `Quantum`
-   `Archived`, plus timestamp fields `CreatedAt`, `UpdatedAt`,
    `ArchivedAt`

Each thread type implements `Clone()`; repositories return deep copies
rather than internal references.

### Message model and routing keys

Messages derive from `Message` / `Message<TPayload>` and include:

-   `Id`, `Timestamp`, optional `CausationId`, `CorrelationId`
-   `Type` which is used as the routing key

If `Type` is not provided explicitly, it is derived from the message
class name by inserting dots between camel-case segments and lowercasing
(e.g., `DomainCreated` → `domain.created`).

### Runtime lifecycle and cancellation model (Target Architecture)

The runtime supports deterministic shutdown, independent component
lifetimes, and scoped cancellation for handler execution.

#### Root runtime lifetime

``` csharp
private readonly CancellationTokenSource _shutdownCts = new();
public CancellationToken ShutdownToken => _shutdownCts.Token;
```

-   The CTS is private.
-   Only the host may call `Cancel()`.
-   Consumers may observe but not trigger shutdown.

#### Component-level lifetime

``` csharp
var componentCts =
    CancellationTokenSource.CreateLinkedTokenSource(runtime.ShutdownToken);
```

Cancellation flows downward from the runtime root.

#### Subscription vs handler execution tokens

Subscription token controls how long the subscription remains active.

Handler invocation receives a derived execution token:

``` csharp
using var handlerCts =
    CancellationTokenSource.CreateLinkedTokenSource(subscriptionToken);

handlerCts.CancelAfter(_handlerTimeout);

await handler(envelope, handlerCts.Token);
```

#### Handler delegate contract

``` csharp
public delegate Task EnvelopeHandler(
    Envelope envelope,
    CancellationToken cancellationToken);
```

The runtime supplies the correctly scoped token.

### Application host pattern (Target Architecture)

The runtime host is the composition root and owns shutdown authority.

Responsibilities:

-   Own a root shutdown CTS
-   Start and stop runtime components
-   Provide a single shutdown pathway
-   Dispose linked CTS instances deterministically

Components:

-   Implement start/stop
-   Begin background work in `StartAsync`
-   Stop accepting work in `StopAsync`
-   Derive per-message execution tokens internally

## Known gaps

-   A production-ready C# application host/service is not yet included.
-   RabbitMQ provisioning beyond tests is not defined.
-   Authentication/authorization for the API is not implemented.
