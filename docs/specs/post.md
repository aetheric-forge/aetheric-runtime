# Constitutional Article — Post

## Article V — Post

### §1. Definition

**Post** is the governed conveyance of Messages between Participants.

Post exists independently of the mechanisms by which Messages are represented, transported, stored, routed, or delivered.

Post preserves the meaning and continuity of an exchange while allowing its implementation to vary.

---

### §2. Purpose

The purpose of Post is to permit Persons, Institutions, Organizations, services, agents, and other recognized Participants to exchange Messages without requiring shared implementation or direct operational coupling.

Every Campus **SHALL** maintain a Post Office through which Post may be exchanged within its institutional hierarchy.

Post exists to support communication, coordination, delegation, notification, request, response, and the exchange of Knowledge.

---

### §3. Message

A **Message** is a bounded communication conveyed through Post.

A Message expresses semantic intent and may contain or reference Knowledge.

Messages **MAY** represent commands, events, requests, responses, errors, notices, or other forms of communication recognized by the Participants.

The meaning of a Message derives from its contract and context, not from the transport used to convey it.

---

### §4. Envelope

An **Envelope** binds a Message to the information required for postal exchange.

An Envelope **MAY** include:

- Sender
- Recipient or Destination
- Message Contract
- Routing Information
- Correlation
- Time
- Provenance
- Delivery Constraints
- Integrity or Confidentiality Evidence

The Envelope is distinct from the Message it carries.

Postal metadata **SHALL NOT** alter the semantic meaning of the enclosed Message.

---

### §5. Participants

A **Participant** is an Identity or Institution capable of sending, receiving, or otherwise taking part in Post.

A Participant **MAY** act directly, through an authorized Principal, or through an institutional Authority or service.

Participation in Post does not, by itself, establish identity, authority, trust, or permission.

The authority to send or receive a Message remains subject to the governing context of the exchange.

---

### §6. Addressing and Routing

Addressing identifies the intended recipient, destination, role, topic, or institutional scope of Post.

Routing determines the path by which an Envelope is conveyed toward that destination.

Addressing is semantic. Routing is operational.

A change in routing or transport **SHALL NOT** change the intended meaning, recipient, or authority of a Message.

Post **MAY** be routed through one or more Post Offices, including Post Offices in different institutional scopes.

---

### §7. Acceptance and Reference

A Post Office accepts an Envelope into postal exchange.

Upon acceptance, the Post Office **SHALL** provide a Post Reference sufficient to identify the accepted Post within the applicable postal context.

Acceptance establishes custody by the accepting Post Office. It does not, by itself, establish delivery, collection, processing, agreement, or successful effect.

A Post Reference identifies an exchange without redefining the Message or Envelope it references.

---

### §8. Custody and Collection

A Post Office is responsible for Post while that Post remains within its custody.

Custody **SHALL** preserve the Envelope, the integrity of the enclosed Message, and the postal information required for lawful collection or onward exchange.

Collection transfers or exposes an Envelope to an authorized recipient or postal participant.

The inability to collect Post **SHALL NOT** be represented as successful collection.

Postal policy **MAY** govern retention, expiry, repeated collection, forwarding, refusal, and disposal.

---

### §9. Delivery Semantics

Delivery semantics describe the guarantees made by a postal exchange.

A Post Office **SHALL** make its applicable guarantees explicit. Such guarantees **MAY** concern ordering, duplication, persistence, expiry, acknowledgement, retry, or collection.

No universal guarantee of immediate, unique, ordered, or successful delivery is implied by the existence of Post.

Participants **SHALL NOT** infer a stronger guarantee than the governing Post Office provides.

---

### §10. Correlation

Correlation identifies a meaningful relationship among Messages or postal exchanges.

Post **MAY** use correlation to associate requests with responses, commands with outcomes, errors with originating exchanges, or multiple Messages with a shared conversation.

Correlation does not merge the identities of the correlated Messages.

Each Message and Envelope remains independently identifiable and retains its own provenance.

---

### §11. Provenance and Integrity

Post **SHOULD** retain sufficient provenance to establish, where applicable:

- Origin
- Sender
- Authority
- Time of Acceptance
- Postal Context
- Message Contract
- Correlation

The provenance of Post is distinct from the truth or authority of the Message it conveys.

A Post Office **SHALL NOT** silently alter a Message in its custody.

Any transformation required for representation or transport **SHALL** preserve semantic meaning and **SHOULD** remain discoverable where material to interpretation or verification.

---

### §12. Access and Confidentiality

Acceptance of Post does not imply unrestricted visibility.

A Post Office **MAY** restrict access, collection, inspection, forwarding, or disclosure according to identity, authority, policy, privacy, security, or law.

Confidentiality, where required, applies to both the Message and sensitive postal metadata.

The existence of routing information **SHALL NOT**, by itself, authorize access to enclosed Knowledge.

---

### §13. Failure

Postal failure **SHALL** remain distinguishable from successful acceptance, routing, delivery, or collection.

A Post Office **SHOULD** preserve sufficient information to identify the failed exchange and the stage at which failure occurred.

Failure handling **MAY** include refusal, retry, expiry, return, dead-letter custody, or notification according to postal policy.

Failure handling **SHALL NOT** silently transform the Message into a different semantic act.

---

### §14. Technology Independence

Post is independent of transport technology, serialization format, storage mechanism, process boundary, network topology, and deployment environment.

The Runtime **MAY** employ one or more transports or providers to realize Post.

Transport-specific concerns **SHALL NOT** become part of a Message Contract unless they are themselves part of the Message's declared meaning.

A conforming implementation **MAY** substitute one transport for another without changing the constitutional meaning of the exchange.

---

### §15. Constitutional Relationship

Post communicates Knowledge but does not establish the truth of that Knowledge.

Post may carry identity Claims, registered facts, archived records, library resources, commands, events, or other Messages without assuming the constitutional responsibilities of Identity, Registrar, Archive, or Library.

The Post Office governs postal exchange. The sender remains responsible for the Message sent, and the recipient remains responsible for actions taken upon receipt.

---

### §16. Invariants

Every postal exchange **SHALL** preserve the following invariants:

- A Message remains distinct from its Envelope.
- Postal metadata does not redefine Message semantics.
- Acceptance does not imply delivery, collection, processing, or agreement.
- Addressing remains distinct from operational routing.
- A Post Reference identifies an exchange within a postal context.
- Messages and Envelopes retain independent identity and provenance.
- Correlation relates Messages without merging them.
- Postal custody preserves Message integrity.
- Failure remains distinguishable from success.
- Delivery guarantees remain explicit rather than assumed.
- Participants remain independent of transport implementation.
- Post remains independent of implementation technology.

No Post Office **SHALL** silently alter the semantic meaning of a Message entrusted to its custody.
