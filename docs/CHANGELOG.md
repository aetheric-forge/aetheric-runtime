v1.0.1 - Transport contract alignment and Unix socket test integration

### High-Level Summary
Primary goals of this update:
1.  **Align transport startup behavior**: ensure pre-start route registration works consistently across transports.
2.  **Integrate Unix socket transport into shared contract tests**: keep tests transport-agnostic.
3.  **Improve lifecycle coverage**: validate host build/start route flow across multiple transport types.

---

### Key Changes Breakdown

#### 1. Transport contract alignment
*   `UnixSocketTransport` now accepts `SubscribeAsync(...)` before `StartAsync` in client mode by queuing subscriptions and flushing them at start.
*   `UnixSocketTransport.PublishAsync(...)` now throws `InvalidOperationException("Transport not started")` before start, matching other transport implementations.

#### 2. Transport-agnostic bus tests
*   `TestMatrix.BusCases()` now includes Unix socket transport in server mode with unique temp socket paths per case.
*   Unix socket matrix cases are gated to non-Windows environments.
*   Existing bus contract tests now pass consistently across available transports without external UDS server orchestration.

#### 3. Host lifecycle coverage
*   Added `HostTests` to verify `AethericHostBuilder.BuildAsync()` route registration and end-to-end publish/handle flow after `StartAsync`.
*   Added Hosting project reference in test project to support host-level contract coverage.

#### 4. Validation
*   Test suite status after changes:
    * `Passed: 25, Failed: 0` on `AethericForge.Runtime.Tests`.

---

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
 
