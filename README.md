# Aetheric Runtime

**Aetheric Runtime** is an event-driven runtime library for building
thread-based domain systems with explicit messaging and repository
abstractions.

It provides:

-   A clean C# domain model (`Domain`, `Saga`, `Story`)
-   A message bus abstraction (`IBroker`, `ITransport`)
-   Pluggable transports (InMemory, RabbitMQ)
-   Pluggable repositories (InMemory, MongoDB)
-   Contract-driven tests for routing and persistence semantics
-   A lightweight FastAPI gateway for HTTP/WebSocket integration

This repository contains runtime building blocks. It does **not**
include a full application host.

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

-   `AethericForge.Runtime.Model`
    -   Domain types and base message classes.
-   `AethericForge.Runtime.Bus.Abstractions`
    -   `IBroker`, `ITransport`, and handler contracts.
-   `AethericForge.Runtime.Bus`
    -   `MessageBroker`
    -   `InMemoryTransport`
    -   `RabbitMqTransport`
-   `AethericForge.Runtime.Repo.Abstractions`
    -   `IRepo` and `FilterSpec`
-   `AethericForge.Runtime.Repo`
    -   `InMemoryRepo`
    -   `MongoRepo`
-   `AethericForge.Runtime.Tests`
    -   Contract tests for bus and repository behavior.

### Python Gateway (`aetheric-runtime-api`)

A FastAPI service that:

-   Publishes command envelopes to RabbitMQ
-   Dynamically exposes HTTP endpoints based on RabbitMQ bindings
-   Streams session-scoped events over WebSocket

The API is intentionally thin. It does not implement business logic.

------------------------------------------------------------------------

## Core Concepts

### Threads

The primary domain entity is `Thread`, with three concrete types:

-   `Domain`
-   `Saga`
-   `Story`

Each thread includes:

-   `Id` (slug-derived if omitted)
-   `Title`
-   `Priority`
-   `Quantum`
-   `Archived` and timestamp metadata

Repositories return deep copies to preserve snapshot semantics.

------------------------------------------------------------------------

### Messages

Messages derive from `Message` or `Message<TPayload>`.

If a routing key is not explicitly set, it is derived from the class
name:

    DomainCreated → domain.created
    StoryUpdated → story.updated

Routing uses dot-delimited topic semantics with `*` and `#` wildcards.

------------------------------------------------------------------------

### Bus Abstraction

-   `IBroker` routes messages by routing key.
-   `ITransport` handles delivery mechanics.

`MessageBroker` wraps a transport and provides:

-   `Publish(Message)`
-   `Emit(routingKey, payload, meta)`
-   `Route(routingKey, handler)`

Transports included:

-   `InMemoryTransport` -- deterministic, ideal for tests.
-   `RabbitMqTransport` -- topic exchange integration.

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

Filtering supports title search, archived state, and thread type.

------------------------------------------------------------------------

## Environment Configuration

Optional integrations are enabled via environment variables:

    RABBITMQ_URL
    MONGO_URI

The Python API also requires access to the RabbitMQ management API for
command discovery.

------------------------------------------------------------------------

## Known Gaps

This repository intentionally does not include:

-   An application host consuming commands and emitting events
-   Exchange/queue provisioning beyond tests
-   Authentication/authorization for the API

It provides runtime primitives, not a finished system.

------------------------------------------------------------------------

## Status

v1.0.0 establishes:

-   Clear architectural boundaries
-   Transport abstraction via `IBroker`
-   Repository contract semantics
-   Deterministic test infrastructure

Future versions may expand host examples, provisioning patterns, and
production hardening.
