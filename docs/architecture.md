# Architecture

This document describes the architecture of the Aetheric Runtime codebase as it exists in this repository. It is reconstructed from the current code and tests.

## Scope and intent

The repository contains two distinct parts:

- A C# runtime library that defines the core domain model ("threads"), a message bus abstraction with transports, and a repository abstraction with backends.
- A Python FastAPI service (`aetheric-runtime-api`) that exposes HTTP/WebSocket endpoints and bridges to RabbitMQ exchanges.

These components are designed to work with external services (RabbitMQ, MongoDB). The C# runtime does not currently include a host/service layer in this repository; it provides building blocks and tests.

## High-level architecture

```
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
```

## C# runtime library

### Projects and responsibilities

- `AethericForge.Runtime.Model`
  - Defines the core domain types (threads) and message/event base classes.
- `AethericForge.Runtime.Bus.Abstractions`
  - Defines the message bus interfaces (`IBroker`, `ITransport`) and handler delegate.
- `AethericForge.Runtime.Bus`
  - Implements `MessageBroker`, which wraps a transport and provides routing helpers.
  - Implements transports: `InMemoryTransport` and `RabbitMqTransport`.
- `AethericForge.Runtime.Repo.Abstractions`
  - Defines the repository interface (`IRepo`) and filter spec (`FilterSpec`).
- `AethericForge.Runtime.Repo`
  - Implements repository backends: `InMemoryRepo` and `MongoRepo`.
- `AethericForge.Runtime.Util`
  - Shared utility functions (slug generation, dictionary helpers).
- `AethericForge.Runtime.Tests`
  - Contract tests for bus and repo semantics, including optional integration cases when environment variables are set.

### Domain model (Threads)

The core entity hierarchy is `Thread`, with concrete types:

- `Domain`: top-level grouping, optional `Description`.
- `Saga`: belongs to a `Domain` via `DomainId`.
- `Story`: belongs to a `Saga` via `SagaId`, includes `Energy` and `StoryState`.

All thread types include:

- `Id` (auto-generated from the title via a slug if omitted)
- `Title`, `Priority`, `Quantum`
- `Archived`, plus timestamp fields `CreatedAt`, `UpdatedAt`, `ArchivedAt`

Each thread type implements `Clone()`; repositories return deep copies rather than internal references.

### Message model and routing keys

Messages derive from `Message` / `Message<TPayload>` and include:

- `Id`, `Timestamp`, optional `CausationId`, `CorrelationId`
- `Type` which is used as the routing key

If `Type` is not provided explicitly, it is derived from the message class name by inserting dots between camel-case segments and lowercasing (e.g., `DomainCreated` → `domain.created`).

`AethericForge.Runtime.Model.Messages.Threads.*` defines event types for thread changes (e.g., `DomainCreated`, `SagaUpdated`, `StoryUpdated`).

### Bus abstraction

- `IBroker` is a thin facade that routes messages by routing key and delegates to a transport.
- `ITransport` defines `Publish`, `Subscribe`, `Start`, and `Stop`.

`MessageBroker` wraps a transport and offers:

- `Publish(Message)`
- `Emit(routingKey, payload, meta)` which constructs a simple message with a dictionary payload
- `Route(routingKey, handler)` which subscribes to a transport

### Transport implementations

- `InMemoryTransport` implements topic-style routing with `.` separators and wildcards (`*` for single segment, `#` for zero or more). It executes handlers sequentially for deterministic tests.
- `RabbitMqTransport` uses a topic exchange (default name `parallel_you_tests`). It creates a transient exclusive queue per subscription and acknowledges messages after handler completion.

### Repository abstraction

- `IRepo` provides `List`, `Get`, `Upsert`, `Delete`, and `Clear`.
- `FilterSpec` supports text (title contains, case-insensitive), `Archived` flag, and type filtering.

Backends:

- `InMemoryRepo`: in-process dictionary with deep-copy semantics.
- `MongoRepo`: stores all thread types in a single collection with a discriminator field `_t`. It supports filtering by title regex, archived flag, and type. Serialization/deserialization is explicit per thread type.

### Tests and contract expectations

Tests in `AethericForge.Runtime.Tests` define the expected behavior for:

- Bus routing semantics, including wildcard handling and start/stop behavior.
- Repository deep-copy behavior and filtering semantics.

Optional integrations are enabled by environment variables:

- `RABBITMQ_URL` enables RabbitMQ transport tests.
- `MONGO_URI` enables Mongo repository tests.

## aetheric-runtime-api (Python)

### Responsibilities

The FastAPI service is a lightweight gateway to RabbitMQ:

- Dynamically exposes HTTP command endpoints based on existing RabbitMQ bindings.
- Publishes command envelopes to a topic exchange.
- Provides a WebSocket endpoint that streams session-scoped events from a topic exchange.

### Command discovery and HTTP routes

On startup, `discovery.py` queries the RabbitMQ management API at `http://localhost:15672/api/bindings/%2F` using fixed credentials (`py` / `py`). It extracts routing keys bound to the `parallel_you.commands` exchange.

For each discovered routing key, `lifespan.py` registers a POST route:

```
POST /api/commands/{routing_key_segments...}
```

Requests are wrapped in an envelope:

```
{
  "meta": {
    "session_id": "...",
    "reply_channel": "session.{session_id}",
    "correlation_id": "...",
    "routing_key": "..."
  },
  "body": { ... }
}
```

The envelope is published to `parallel_you.commands` using the routing key.

### WebSocket event streaming

`/ws?session_id=...` binds an exclusive queue to the `parallel_you.events` topic exchange with routing key `session.{session_id}.#`. Messages are forwarded to the client as text frames.

### Configuration

- `RABBITMQ_URL` must be set for RabbitMQ connections.
- The RabbitMQ management API must be reachable at `http://localhost:15672` with user `py` / `py` for command discovery.

## Cross-cutting concerns and conventions

- Topic routing: both the C# runtime and Python API assume dot-delimited routing keys with `*` and `#` wildcard semantics.
- Session-scoped events: the API uses routing keys prefixed with `session.{session_id}` for event delivery.
- Deep copies in repositories: clients should assume the repo returns immutable snapshots of stored threads.

## Known gaps (in this repo)

- No application host for the C# runtime is included (no service that consumes commands and emits events).
- RabbitMQ exchange/queue provisioning beyond tests is not defined here.
- Authentication/authorization for the API is not implemented.

