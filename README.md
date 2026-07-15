# Aetheric Forge Runtime

**Aetheric Forge Runtime** is a constitutional runtime for composing durable, autonomous Institutions.

The Runtime models systems in terms of identity, knowledge, authority, institutional purpose, and governed capability. Constitutional specifications define what each Institution is responsible for; public contracts and replaceable infrastructure determine how those responsibilities are realized in software.

Version 2.0 establishes the core institutional architecture of the Forge.

---

## Status

The **2.0 milestone is complete**.

The current release establishes:

- the constitutional ontology of Identity, Knowledge, Institution, Post, and Campus;
- the Archive, Post Office, Registrar, and Library Institutions;
- institutional contexts and parent hierarchy;
- scoped capability registration and hierarchical resolution;
- specialized Authorities owned by their Institutions;
- replaceable services, providers, transports, storage, and serialization boundaries; and
- a tested asynchronous institutional lifecycle.

The unit-test suite is passing. End-to-end validation and release packaging are performed as release activities against composed deployments.

---

## Design Principles

### Constitution before implementation

Constitutional specifications define institutional purpose, authority, relationships, and invariants independently of programming language or infrastructure.

Code implements those specifications; it does not silently redefine them.

### Institutions are the unit of composition

An Institution has a specialized public contract, an institutional context, a place in an institutional hierarchy, and an explicit lifecycle.

Specialized requirements remain on specialized Institutions rather than accumulating on a universal base interface.

### Capabilities follow institutional scope

Institutions register other Institutions as capabilities within their scope.

Resolution searches locally and then through successive parent scopes. A local registration shadows an inherited registration, allowing descendants to specialize their environment without modifying their ancestors.

### Authorities belong to their Institutions

An Archive owns its Archivist. A Post Office owns its Postmaster. A Library owns its Librarian. A Registrar owns its registration authority and supporting machinery.

The containing scope registers the Institution, not each internal Authority, clerk, service, or provider.

### Infrastructure remains replaceable

Storage, transport, serialization, identity integration, persistence, and hosting enter through narrow contracts.

Technology may change without changing institutional identity or constitutional meaning.

---

## Constitutional Model

The constitutional corpus is maintained in [`specs/`](specs/).

| Article | Subject | Constitutional concern |
| --- | --- | --- |
| Preamble | Ontology | Common language and constitutional foundation |
| I | Identity | Persistent representation, authentication, authorization, Claims, and trust |
| II | Knowledge | Artifacts, representations, lineage, references, provenance, and attestation |
| III | Institution | Purpose, governance, organization, policy, capability, and continuity |
| IV | Archive | Historical integrity, provenance, immutability, and institutional memory |
| V | Post | Messages, Envelopes, postal exchange, custody, routing, and delivery semantics |
| VI | Campus | Root hierarchy, institutional boundary, and required Campus capabilities |
| VII | Registrar | Authority, recognition, attestation, amendment, and revocation |
| VIII | Library | Collection, curation, discovery, access, plurality, and stewardship |

Specifications are normative. Architecture documents explain how the Runtime represents and composes the concepts they define.

---

## Core Institutions

### Campus

A Campus is the root Institution of an institutional hierarchy. It has no parent and supplies the shared scope within which descendant Institutions operate.

A Campus maintains:

- an Archive;
- a Library;
- a Post Office; and
- a Registrar.

These are Campus requirements, not universal properties of every Institution.

### Archive

The Archive preserves institutional history, provenance, and constitutional continuity. It owns its Archivist and coordinates storage through vault and provider contracts.

### Post Office

The Post Office exchanges Post within and across institutional scopes. It separates Message and Envelope semantics from transport-specific delivery.

### Registrar

The Registrar establishes authoritative institutional recognition. It records and attests facts without creating identity merely by registering them.

### Library

The Library curates Knowledge for discovery, interpretation, teaching, and reuse. Collection does not imply endorsement, constitutional authority, or historical truth.

---

## Institutional Capabilities

Each Institution owns a local capability scope.

```csharp
mainCampus.Register<IArchive>(mainArchive);
mainCampus.Register<IPostOffice>(mainPostOffice);
mainCampus.Register<IRegistrar>(mainRegistrar);
mainCampus.Register<ILibrary>(mainLibrary);
```

A descendant resolves the nearest matching Institution:

```csharp
var postOffice = scienceLibrary.Resolve<IPostOffice>();
```

Resolution follows the institutional parent hierarchy:

1. Search the current scope.
2. Return a local registration when present.
3. Otherwise search the parent scope.
4. Continue until a match is found or the Campus root is exhausted.

This allows a Science Faculty Library to use the Main Campus Post Office while permitting the Faculty to register a more local Post Office later.

---

## Runtime Model

Institutions share an asynchronous lifecycle:

```text
Constructed → Initialized → Started → Stopped
```

- **Construction** creates an Institution and its owned collaborators.
- **Registration** makes an Institution visible as a capability within a scope.
- **Initialization** validates completed composition and prepares operation.
- **Start** begins active institutional work.
- **Stop** quiesces operation and releases owned runtime resources.

Institutional capability resolution and dependency injection are intentionally separate:

- institutional resolution discovers Institutions through constitutional scope;
- dependency injection supplies implementation collaborators.

---

## Architecture

The architecture documentation is divided into focused documents:

- [Architecture Overview](docs/architecture/overview.md) — system map, principles, layers, and boundaries.
- [Institutional Model](docs/architecture/institutional-model.md) — Institution contracts, contexts, hierarchy, registration, resolution, and Campus composition.
- [Runtime Model](docs/architecture/runtime-model.md) — composition, lifecycle, Authorities, teams, clerks, services, cancellation, and failure.
- [Infrastructure](docs/architecture/infrastructure.md) — providers, vaults, transports, serialization, persistence, security, reliability, observability, and deployment.

The former monolithic `architecture.md` described the 1.0 runtime and has been superseded by this documentation set.

---

## Repository Layout

The repository is organized around three complementary forms of truth:

- **Specifications** define constitutional purpose and invariants.
- **Runtime contracts and implementations** realize the institutional model.
- **Tests** verify public behaviour, lifecycle, delegation, and provider contracts.

Key documentation locations:

```text
specs/                  Constitutional specifications
docs/architecture/      Architecture documentation
LICENSE.md              Public licence terms
CONTRIBUTOR-LICENSE-AGREEMENT.md
                        Contribution and relicensing terms
```

Source and test projects are described by the solution and project files in the repository.

---

## Building and Testing

Use the .NET SDK version selected by the repository configuration.

```bash
dotnet restore
dotnet build
dotnet test
```

The unit tests exercise institutional contracts, lifecycle, delegation, capability registration and resolution, Archive services, Authorities, and provider behaviour.

Tests requiring external infrastructure should declare their own environment prerequisites and remain distinguishable from deterministic unit and contract tests.

---

## Licensing

Aetheric Forge is made available under the [Aetheric General License](LICENSE.md).

Copyright in the original work through the 2.0 milestone is held by Brian Richardson. The Aetheric Forge Initiative acts as project steward, and Black Circuit Design Inc. is authorized to offer alternative licence terms.

Contributors retain ownership of their Contributions while granting the rights required to maintain, distribute, and dual-license the combined work. See the [Contributor Licence Agreement](CONTRIBUTOR-LICENSE-AGREEMENT.md) before submitting a Contribution.

---

## Project Stewardship

The Aetheric Forge Initiative develops and stewards the constitutional specifications and publicly licensed Runtime.

Black Circuit Design Inc. supports alternative licensing and commercial application of the work.

The Forge is intended to remain useful across implementations, organizations, and deployment environments without surrendering the clarity of its constitutional model.

---

*Forge freely. Share deeply. Guard the light.*
