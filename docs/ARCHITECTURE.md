# Parallel You Architecture v1.0

> _A simple, opinionated thought organizer_

## 1. Purpose & Philosophy

Parallel You was developed primarily to help myself think. I took this need as an opportunity to share this architectural idea that has been coming together for quite some time. I hope this architecture can be of use to small organizations who want a huge technology and efficiency advantage against much larger competitors. It is also suitable for any other small organization looking to cut costs. And finally, as a template for larger scale deployments. Projects accumulate rapidly, and a dashboard to visualize time and effort helps immensely in planning. The secondary purpose is to demonstrate its architecture and offer examples on how to reuse it in other applications.

### Message-shaped reality

Parallel You functions by exchanging `Messages` between components. Each component is updated of the requests and updates from interesting components. Messages are exchanged over a _Bus_, and an individual component can stay up-to-date with its peer components. Components do not require knowledge of each other, only that their messages conform to the specification, and that it has passed through the bus. More components can be added in a non-disruptive fashion by creating new services and commands and events, and simply attaching them. If there is a service that responds to a command, the bus will find it, and if the client is configured to send it and listen for response events, then the whole system remains agnostic of its individual components.

### Intentional minimalism

This architecture scales immensely. Experience will tell you that scaling an application becomes extremely difficult after a critical mass is reached if the architectural foundation is not solid. Refactoring and adding new features becomes increasingly difficult as developers get overwhelmed with maintenance tasks, and small bugfixes are difficult to make, and must go through a full process to be released.

By reducing the number of components to maintain, the system can be maintained by as few as 1 or 2 people, and rapidly improved. Reduced maintenance requirements and well-structured and readable code allow software to last decades instead of just a few years. In a world with emerging AI, long-lived systems with extensive domain-specific memories and years of machine learning will be one of the biggest payoffs of building systems meant to last, not be recycled.

### Self-assembling interfaces

Redundancy is one of the more common culprits when bugs and incoherence creep in. If each component must maintain its own interface and data contracts, the chance for mismatch and unintentional consequences is high. Therefore, there is a single source of truth for the API - the bus. A simple Python FastAPI application will discover the intended web API by introspecting the RabbitMQ exchange for the application. By finding queue bindings, routing keys can be discovered, and an API built from the routing keys. Additionally, the minimal FastAPI application is much easier and less frequently maintained than a verbose API specification. If the bus is the contract, then the API is always in sync with the domain.

### Commands in, Events out

Clients attach to the web broker with a unique session ID. The web broker is connected to the **commands** queue. Upon receiving a message from a connected client, the web broker will transport the raw JSON to the command queue after verifying the message envelope. The web broker is also connected to the **events** queue, as a consumer. When a command handler processes the command, it will emit _Events_ in response. For example, the command `thread.list` might be sent with any necessary parameters, and the **ThreadService** would process the command, and emit a `threads.listed` event in response. The web broker would then look at the event envelope and push the event back to the connected client via WebSockets.

### No duplicated schemas

Another way to reduce redundancy is to keep a single source of truth for domain and interface contracts. In Parallel You, the single source of truth is the domain model. The messages contained within the domain model will be mapped back to the message bus topics, and then the FastAPI application will introspect the message bus to infer the web API.

### System-as-nervous-system

The system can be visualized as a nervous system. Key nodes (microservice entrypoints, client web broker) have trees of nodes behind them, and small packets pass between nodes to exchange information and commands. The human nervous system functions in very much the same way - a nervous impulse (tiny molecules) travels from the central nervous system (service endpoint) to the appropriate organ (microservice), which will emit hormones (events) or additional impulses (fan-out) as a result. The human nervous system works because small, simple signals move between loosely coupled structures. Parallel You mirrors this: tiny messages flow between independent services, and the system coordinates itself.

## 2. System Topology

The entire stack is presented here, and the rationale for the specific technology chosen presented. It may well be that there are use cases in which other technologies are more appropriate to the problem at hand, which is why the system has been designed to be easily extended in both the Repo and Bus components.

### API Layer (FastAPI)

FastAPI is a very simple approach to Web APIs. Python is plenty capable of supporting a thin proxy to a message queue and dispatching events. And, of course, it can do any other Pythonic things. However, the idea is that the API should not do any heavy lifting, merely orchestration and brokering. The FastAPI application provided introspects the current state of the application's Bus exchange to infer the required Web API - it will always be in sync with the backend.

### Domain Services (C# v9 microservices)

Experimentation with Python led me to conclude that its async model struggled under sustained high-concurrency RabbitMQ workloads. C#'s true async runtime fit the continuous message-consumption pattern better.

### Repository Layer (Mongo or Memory)

During early development, the in-memory repository was very useful for rapid iteration. Of course, the point of Parallel You is persistence, so I decided that a document database most closely matched the data structures I was intending to persist. Indeed, these documents were already in JSON after being serialized to the bus, so persisting JSON to MongoDB was a natural conclusion. Again, not the only one, but a very good choice.

### UI Layer (Blazor WASM)

A surprising dark-horse tech from Microsoft, but Blazor is really the only framework that realistically allows for true async in a browser sandbox. It still has to pipeline WASM primitives to a single-threaded backend, but at least the browser will give you async. React and similar frameworks fight the browser's single-threaded model; Blazor aligns with it.

### Event Stream (WebSocket-based session pumps)

We are obviously not going to connect directly from the UI layer to the message queue, so there needs to be a mechanism by which the web broker can emit events back to the client. WebSockets is perfect for this. It can also be secured with SSL later.

## 3. Envelope Protocol

The envelope protocol allows junction nodes such as the web broker and service entrypoint to quickly route messages without parsing the body.

### Envelope shape

The envelope consists of 2 properties, the `Meta` field, and the `Body` field. Envelopes are immutable; handlers do not modify incoming metadata, only produce new events with new metadata.

### Meta block

The `Meta` block encompasses the following parameters:
* **Routing Key** - The destination command handler for the `Body`.
* **Reply Channel** - The routing key to use in any response event emission.
* **Correlation ID** - Allows the web broker to correlate response events to the original command.
* **Session ID** - Allows the web broker to route the response to the correct client.

### Body block

The body block contains a JSON structure that will be parsed by the command handler. The JSON body should take the shape of a class that can be constructed in the C# service.

### Session targeting

The client session ID is included in the command so that the response events can be correctly routed back.

### Correlation IDs

A unique ID, called a correlation ID, is used by the web broker to track outgoing commands so that any incoming response events can be correlated back to the original command.

### Reply channels

The reply channel is the routing key used by any response event emission.

## 4. Command/Event Model

_Messages_ are categorized in two broad categories. _Commands_ are sent from a node out to the bus, where it is picked up and routed to the internal command handler via the service entrypoint. _Events_ are the complements of Commands, and are emitted in response.

### Message → Command → Event hierarchy

_Message_ acts an abstract base class for both `Command` and `Event`, and contains the following properties:

* A unique ID
* The routing key
* Outgoing timestamp
* A unique correlation ID
* The correlation ID provided in the incoming command
 
The correlation ID generated for a command is echoed into all resulting events as the causation ID, allowing clients to trace outcomes without inspecting payloads.

### Routing keys

The service entrypoint uses the routing key to pass the Message to the appropriate service or endpoint.

### Intent and Outcome

Commands represent intent: a client asking the system to perform a behavior.
They are imperative, directional, and always consumed by exactly one handler.

Events represent outcome: the factual result of domain logic.
They are descriptive, broadcast-oriented, and may have multiple listeners.

This separation ensures that the meaning of a message never depends on who
receives it. Commands request change; events describe change.

### Idempotence expectations

Command handlers should be designed to behave idempotently: repeated delivery of the same command by unintentional redelivery or otherwise, must not cause unintended duplication, or inconsistent state.

### Serialization strategy

Command handlers deal with first class objects. The thread entrypoint is responsible for reconstructing the command object from the provided JSON. Therefore, clients are responsible for submitting the required API object in JSON form. Likewise, clients are expected to construct their own models from the emitted JSON. The Command handler can remain agnostic of the client's models.

## 5. Dynamic API Generation

To this point, the underlying infrastructure has a single layer and a single model. Fully tested code here should not be brittle, and should be extensible exactly as described thus far. The API layer is the first opportunity for brittleness and chaos to creep in. If the API layer is out-of-sync with the backend expectations, it can lead to all kinds of undesired behavior. The dynamic web API generation was an elegant solution to a common problem. The API cannot be out of sync with the backend if it uses the same source of truth: the message exchange command bindings. By constructing the API from the command bindings, the web API will mirror the expectations of the receiving service entrypoint.

### No controllers?

So, no explicit controllers; they are introspected from the bus exchange. The system still has controller-like behaviors, but they are generated dynamically rather than hand-authored.

### No explicit endpoints?

With no controllers, there is no need to define explicit routes.

### RabbitMQ API introspection drives route creation

The `discover_endpoints()` method in `parallel_you_api.discover` will use the RabbitMQ API to query the exchange for its current bindings, and create a command map which will be converted into an explicit route map for FastAPI.

### Path construction via routing keys

The routing keys themselves produce a path structure that mirrors the form `<controller>.<method>`. For example, the command `thread.start` would be converted into a route of `/api/thread/start` and accept the parameters via the allowed methods (default POST-only).

### JSON Passthrough

The web API should not do any processing on the incoming JSON except to route it to the appropriate bus topic. The Command handler alone is responsible for model object construction.

### Contract consolidation

The overall result is that the message contract must only be defined in a single location: the service's object model. It is translated once by the application into the bus exchange in the form of command bindings, and the Dynamic API Generation will ensure that the contract is enforced from the web client side.

## 6. The Service Pattern

Traditional software patterns are highly structured and centralized: they assume a single locus where behavior is defined, and everything else is arranged around that frame. As these systems grow, the central structure becomes a bottleneck; change has to radiate outward from one place, and the architecture eventually resists the weight of the application.

Parallel You takes the opposite approach. Each service defines its own piece of the system by declaring the commands it understands and the events it emits. The architecture behaves more like a fabric than a frame: new behavior is introduced simply by adding a new command handler, binding it to the message bus, and letting the rest of the system discover it.

If a new domain area is required, a service for it can be created independently: give it a unique domain prefix, attach it to the bus, publish its command bindings, and start emitting events. The API discovery process will detect the new commands automatically and expose them over HTTPS and WSS without requiring any changes to existing code.

### Command handlers discovered by reflection

Each domain service inspects its own assembly at startup and discovers all classes implementing `ICommandHandler<in TCommand>`. This reflection pass produces a registry of available commands, keyed by their routing keys. The routing key is derived from the service’s domain prefix and the command’s name. For example, a `ThreadService` using the prefix `thread.` and a `CreateSaga` command handler would naturally define the routing key `thread.create.saga`.

When a message arrives at the service entrypoint with a matching routing key, the entrypoint deserializes the JSON payload into the appropriate command type and delegates execution to its corresponding `HandleCommandAsync()` method.

### Self-registration via RoutingKey

The result of the process described above is that the application emerges as each domain service registers its command handlers on the bus. No central routing table or registry is required; each service declares the commands it understands simply by binding its handlers to the message exchange. Once these bindings exist, the rest of the system adapts automatically: the API layer discovers the new routing keys, and clients can begin sending commands without modifying any existing code.

This allows the system to grow in multiple directions at once, with new capabilities introduced wherever they naturally belong.

### Dispatch model

By implementing a dispatch model in both the web broker and the service entrypoint, both components can remain agnostic to the semantics of the command/event exchange. The entrypoint receives a command, reconstructs the corresponding command object from JSON, and dispatches it to the appropriate asynchronous command handler based solely on the routing key.

Similarly, the web broker subscribes to all relevant events and dispatches them to the target client session(s) based on the reply channel and session ID in the envelope. Neither side needs to understand the domain; they only need to trust the envelope and the routing rules.

### Worker minimalism

This design keeps workers both highly specialized and extremely minimal. Each command is handled by a short-lived worker whose only responsibility is to execute the requested behavior and emit any resulting events. All plumbing—queue bindings, routing logic, envelope validation, and dispatch—is handled by the service entrypoint. As a result, workers contain no infrastructure code and no messaging boilerplate; they exist purely to implement domain logic.

### Event publication

Unlike a traditional request–response protocol (e.g., gRPC), the publish–subscribe model enables richer interaction patterns between services and clients. A single command may trigger a sequence of domain actions, all carrying the same session ID and correlation ID. Each resulting event can be published as soon as it occurs, allowing the originating client to receive incremental updates rather than waiting for a single response.

Because events are broadcast to any subscriber interested in the routing key, multiple clients can observe the same state changes in real time. This enables shared dashboards, collaborative views, and synchronized UI updates without any additional work in the domain services.

## 7. Repository Abstraction

Persistent storage is a key feature of any long-lived system. At the application level, I/O is always an abstraction—whether a hardware device, a file, or a remote database—and this architecture is no exception. Parallel You keeps this layer intentionally modest. The repository pattern has a mixed reputation in modern software design, but much of that comes from implementations that attempted to do far more than the pattern was designed for.

In Parallel You, a repository is a boundary object. It provides a stable, predictable interface for loading and saving domain models, and nothing more. The internal mechanics—serialization choices, storage engines, indexing strategies—are treated as replaceable details rather than architectural commitments. By narrowing the responsibilities of the repository, we avoid the common pitfalls: leaky abstractions, accidental business logic, and hidden coupling to the underlying persistence technology.

This section explores how and why the repository remains part of the design, what constraints keep it intentionally simple, and how those constraints reinforce the core principle of Parallel You: domain behaviour drives storage, not the other way around.

### Still Repos?

The idea of abstracted I/O has been around for a very long time. This architecture makes no attempt to be revolutionary in that respect; it simply acknowledges that the pattern is often misinterpreted as an invitation to process domain objects rather than persist them.

### But simple Repos

By keeping the interface minimal (`get`, `list`, `upsert`, `delete`, `clear`), the semantics remain focused on storing and retrieving serialized JSON data. Any backend capable of writing and reading JSON text is a sufficient implementation of this interface, whether it is a database, a flat file, or another durable medium.

### Persistence doesn't modify domain design

Because the repository is agnostic to domain contracts, it has no influence on how the domain model evolves. Its sole requirement is that domain objects can be serialized to JSON and restored from it. Domain decisions stay in the domain.

### Identical public contracts

Every domain service relying on the repository interface interacts with the same small, stable set of methods. These methods are tested thoroughly, ensuring that all implementations behave consistently and remain replaceable without collateral changes.

## 8. UI Event Loop

The runtime is UI-agnostic; any asyncio-compatible event loop can attach itself as a client. A UI integrates by registering interest in specific event types and reacting to the backend’s WebSocket event stream.

The general flow:

1. For each event type the UI cares about, it registers an async handler or awaits a dedicated Future/Task tied to that event.

2. The UI opens a WebSocket session using a unique session ID. The web broker uses this ID to route events (unicast or multicast) to the correct UI client channel.

3. When the backend publishes an event on the Runtime Bus, the web broker forwards it to all subscribed UI sessions.

4. The UI event loop receives the event and dispatches it to the associated handler/Future, which drives screen updates, state changes, or additional commands back into the system.

### Session-based event routing

Each UI client establishes a long-lived WebSocket (WSS) connection at startup. As part of the handshake, the UI provides a unique session ID (GUID or ULID), which identifies that UI instance for the duration of the connection.

The web broker uses this session ID as the routing key for outbound events. When a backend event arrives, the broker evaluates it against its subscription table and delivers it to all interested UI sessions via unicast or multicast. As long as the WSS connection remains active, the session ID remains bound and continues to receive its subscribed events.

### Live-updating UI via WebSockets

UI clients receive live updates exclusively through their WebSocket session. WebSockets provide a push-based channel, so the UI does not poll the backend; events are delivered as soon as the runtime publishes them. When a subscribed event arrives, the UI’s handler reacts immediately and updates its display or state.

### Projection logic in UI only

The backend emits events strictly according to the API contract. These payloads are canonical and not tailored to UI presentation. If the UI requires alternate view models or derived representations, it is responsible for projecting the incoming event data into its own local model. No backend projection or UI-specific shaping is performed.

## 9. Operational Notes

Operational behavior in Parallel You is defined by a small set of guarantees that hold regardless of deployment topology or UI implementation. These notes describe how the system behaves under load, in the presence of failure, and during ongoing event flow.

### Scaling horizontally

Parallel You is designed to run multiple backend instances without shared in-process state.  
All persistent state resides in external repositories, and all inter-component interactions occur through events. As a result:

- Additional backend nodes may be started or stopped without coordination.
- Any node may handle a command and emit the resulting event.
- The web broker uses the UI’s session ID to route outbound events; no node requires UI affinity.
- No runtime component assumes single-node exclusivity.

Horizontal scaling becomes a matter of adding nodes, not redesigning the system.

### Compensating behaviors

Longer-running workflows may require more than one command to complete a logical action.  
To preserve system integrity when such workflows fail, Parallel You uses **workflow orchestrators**:

- A workflow orchestrator issues a sequence of commands as part of a multi-step operation.
- If a later step fails, the orchestrator may emit compensating commands to restore a valid state.
- Inconsistencies are surfaced as explicit events, not hidden or silently resolved.
- Compensation is business-level correction, not transport-level retrying.

This ensures that partial success does not leave the system in an undefined or unrecoverable state.

### Contention & concurrency

Parallel You does not provide global serialization.  
The system operates under optimistic concurrency:

- Multiple commands may target the same model; the repository is responsible for conflict detection (versioning, timestamps, or other means).
- A detected conflict produces a shaped error event rather than a crash or undefined behavior.
- Event ordering is guaranteed only **per session**, not globally across the system.
- UI clients must tolerate interleaving events from unrelated models.
- Backend handlers must be idempotent, since events may be delivered more than once.

Concurrency is treated as a normal condition, not an error.

### Observability (events > logs)

Events are the primary record of system behavior.  
If an action is meaningful to the system, it must appear as an event.

- Events form the audit trail; logs are supplemental and best-effort.
- Observability tools may subscribe to the event stream just as UI clients do.
- No component relies on local logs for correctness, replay, or reconstruction.
- Error, success, and workflow progress are all expressed as events.

This model provides consistent observability without depending on centralized log aggregation.

### Error shaping

Errors are represented as structured events and follow a predictable API contract.

- Exceptions within handlers are converted into typed error events.
- Error events include fields such as error code, message, context, and correlation ID.
- Errors that affect workflow progression are surfaced explicitly; they are never silently absorbed.
- UI clients treat error events exactly like other events, projecting them as needed.

This ensures that failures are expressed consistently and can be reacted to uniformly.

### Retry behavior

Retry logic in Parallel You is intentional, transparent, and bounded.

- UI WebSocket clients automatically reconnect; the session ID persists across reconnects.
- Backend command handlers do not retry implicitly; retries occur only when driven by a workflow orchestrator.
- Duplicate event delivery is possible; handlers must be idempotent.
- Workflow orchestrators define their own retry policies, including backoff and maximum attempts.
- At-least-once delivery is assumed; exactly-once semantics are not guaranteed.

Retries are treated as a controlled part of workflow logic rather than a hidden property of the transport.


## 10. Extending the System

### Adding a command type

### Adding a handler

### Adding event emission

### API and UI auto-update

### The system _breathes_ around new capabilities

## 11. Security

### RabbitMQ policies

### Session event scoping

### Envelope sanitization

### Event-only data leaving domain services

### Zero trust between layers except message contracts

## 12. Credit & Licensing

### AGL Principles

### Required attribution

### Influence notes

### Canonical patterns

### How to reuse responsibly
