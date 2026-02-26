v1.0.0 - Re-architected runtime core

### High-Level Summary
Primary goals of this release:
1.  **Clarify runtime architecture**: Align docs and code around the envelope-based bus, generic repos, and minimal host.
2.  **Strengthen abstractions**: Standardize the bus (`IBroker`, `ITransport`) and repo (`IRepo<T>`) boundaries.
3.  **Stabilize tests**: Keep deterministic, backend-agnostic contract tests for transports and repositories.

---

### Key Changes Breakdown

#### 1. Envelope-based Bus
*   `Envelope` and `EnvelopeKind` are the core message carriers.
*   Routing keys are derived from envelope fields via `RoutingHelpers`:
    * Request: `{service}.{verb}`
    * Event: `{topic}`
    * Response/Error: `reply.{client_id}`

#### 2. Minimal Host Layer
*   `AethericHost` starts/stops transports and exposes the broker and repos.
*   `AethericHostBuilder` composes transports, repos, and handlers.
*   Handler patterns include `ICommandHandler<T>`, `IEventHandler<T>`, and explicit envelope routes.

#### 3. Repo Abstractions and Backends
*   `IRepo<T>` and `IFilterSpec` define generic storage.
*   `InMemoryRepo<T>` and `MongoRepo<T>` provide concrete backends.

#### 4. Tests
*   `TestMatrix` runs bus and repo contract tests across optional backends.
*   `BusTests` verify topic matching, wildcard routing, and start/stop behavior.
*   `RepoTests` verify deep-copy semantics, upsert, delete, and list behavior.
 
