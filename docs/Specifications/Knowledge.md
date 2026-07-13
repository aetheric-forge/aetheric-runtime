# Article II — Knowledge

## Knowledge

**Knowledge** is information made intelligible within the Forge through structure, context, and relationships.

Knowledge is not limited to facts or assertions. It may include observations, instructions, designs, records, creative works, executable expressions, and other forms of meaningful information.

Knowledge is independent of any particular encoding, storage medium, transport, or service provider.

## Knowledge Artifact

A **Knowledge Artifact** is a discrete, identifiable unit of Knowledge.

An Artifact may be created, referenced, exchanged, preserved, revised, derived, or composed with other Artifacts. It provides the stable conceptual boundary around which identity, provenance, integrity, and relationships are expressed.

An Artifact is not necessarily a file or document. It may represent any sufficiently bounded body of Knowledge.

## Representation

A **Representation** is a concrete expression of a Knowledge Artifact.

An Artifact may have multiple Representations, including different encodings, formats, languages, resolutions, or materializations. Representations may differ while expressing the same underlying Artifact.

A Representation belongs to an Artifact but does not, by itself, define the Artifact’s identity.

## Immutability

A Knowledge Artifact is immutable.

Once established, its content and intrinsic metadata cannot be altered. Any change produces a new Artifact with its own identity.

Immutability does not require an Artifact to remain available forever. It requires that an Artifact cannot be replaced by different Knowledge while retaining the same identity.

## Lineage

**Lineage** records the relationships through which one Artifact originates from, transforms, supersedes, or incorporates another.

An Artifact may identify zero or more predecessor Artifacts. Consequently, lineage may form a chain, branch, merge, or directed acyclic graph.

Lineage relationships are themselves meaningful Knowledge and must be independently verifiable.

## Revision

A **Revision** is an Artifact related to an earlier Artifact by an explicit revision relationship.

A Revision does not modify or replace its predecessor. It expresses a newer state, interpretation, or edition while preserving the predecessor as part of its lineage.

No Revision is intrinsically “current.” Currency is established by an authoritative reference, publication, or institutional policy.

## Derivation

A **Derived Artifact** is produced using one or more existing Artifacts as sources.

Derivation includes revision, translation, transformation, compilation, aggregation, extraction, and other processes that produce distinct Knowledge from existing Knowledge.

Where known, a Derived Artifact should identify both its source Artifacts and the process responsible for its creation.

## Reference

A **Reference** identifies or resolves to a Knowledge Artifact.

A Reference may be:

- **Fixed**, identifying one specific immutable Artifact.
- **Symbolic**, resolving to an Artifact according to an authority and resolution context.

A fixed Reference remains stable because its target cannot change. A symbolic Reference may resolve differently over time without altering the identity or content of any Artifact involved.

References do not imply ownership, endorsement, truth, or authority.

## Authority

An **Authority** is an Identity recognized within a particular context as competent to make assertions about Knowledge.

An Authority may publish Artifacts, establish symbolic References, designate preferred Revisions, attest to provenance, or withdraw its endorsement of previously published Knowledge.

Authority is contextual rather than universal. An Identity may be authoritative for one purpose, domain, or Institution without being authoritative for another.

Recognition of authority does not make an assertion objectively true. It identifies who made the assertion and the context in which it is accepted.

## Authoritative Reference

An **Authoritative Reference** is a symbolic Reference maintained by an Authority.

It designates the Artifact that the Authority presently recognizes for a stated role, such as the current revision, approved release, canonical schema, or governing policy.

Updating an Authoritative Reference changes its resolution but does not modify, replace, or invalidate any previously referenced Artifact.

Resolution should therefore be understood as contextual:

> Reference + Authority + Context → Artifact

## Provenance

**Provenance** describes the known origin and history of a Knowledge Artifact.

Provenance may identify:

- The Identity responsible for creating the Artifact.
- The time and context of its creation.
- The source Artifacts from which it was derived.
- The process or transformation used to produce it.
- The Institution through which it was published.
- Relevant attestations made by other Identities.

Provenance is expressed through verifiable claims. It need not be complete for an Artifact to exist, but missing or uncertain provenance must not be represented as known.

## Claim

A **Claim** is an assertion made by an Identity about an Artifact, another Identity, or a relationship between them.

Claims may concern authorship, derivation, ownership, approval, classification, integrity, or any other meaningful property.

A Claim records what an Identity asserts. It does not independently establish that the assertion is true.

Claims are themselves Knowledge Artifacts and are therefore immutable, identifiable, and capable of carrying their own provenance.

## Attestation

An **Attestation** is a signed Claim through which an Identity accepts responsibility for an assertion.

An Attestation binds:

- The asserting Identity.
- The Claim being made.
- The Artifact or relationship concerned.
- Any applicable scope or context.
- The cryptographic evidence required for verification.

An Attestation may later be superseded, disputed, or revoked, but it cannot be retroactively altered.

## Revocation

A **Revocation** is an Artifact declaring that an Identity no longer stands behind a previous Attestation or Authoritative Reference.

Revocation does not erase the original assertion or make it cease to have existed. It adds new Knowledge that changes how the assertion should be evaluated from that point forward.

History accumulates; it is not rewritten.

## Relationship

A **Relationship** is a typed association between two or more Knowledge Artifacts.

Relationships may express derivation, revision, dependency, reference, annotation, contradiction, equivalence, inclusion, or other meaningful connections.

A Relationship:

- Identifies each participating Artifact by fixed Reference.
- Declares the nature and direction of the association.
- May include contextual information about its scope or meaning.
- Is itself a Knowledge Artifact.

Relationships do not modify their participants. Conflicting, superseding, or additional Relationships accumulate as further Knowledge.

## Composition

A **Composition** is a Knowledge Artifact whose meaning or function depends upon an explicitly identified set of constituent Artifacts.

A Composition identifies:

- Its constituent Artifacts.
- Their roles within the Composition.
- Any ordering or structural relationships between them.
- The rules required to interpret or materialize the whole.

Composition does not merge or mutate its constituents. Each constituent retains its own identity, provenance, and lineage.

Because a Composition refers to immutable Artifacts, it describes a reproducible state. A changed constituent or structure produces a new Composition with its own identity and lineage.