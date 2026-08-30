# Constitutional Article — Identity

## Article I — Identity

### §1. Definition

**Identity** is the persistent and governable representation of a Subject within the Runtime.

It establishes continuity across interactions, credentials, sessions, providers, and changes of state. An Identity may represent a person, organization, service, device, agent, process, or any other Subject capable of recognition or participation within the Runtime.

Identity is distinct from the evidence used to establish it, the Principal acting through it, and the permissions granted to it.

---

### §2. Purpose

The Identity domain provides the contracts by which the Runtime:

- identifies and distinguishes Subjects;
- associates Subjects with persistent Identities;
- authenticates presented credentials;
- establishes Principals and authentication sessions;
- issues, evaluates, and revokes Claims;
- authorizes actions through Roles and Permissions;
- resolves Identities through external or internal providers;
- governs Identity state and lifecycle;
- and represents trust between issuers, realms, and authorities.

---

### §3. Identity Model

#### Subject

A Subject is that which may be identified.

A Subject may exist independently of the Runtime and independently of whether the Runtime has assigned it an Identity. Subjects may include natural persons, legal persons, institutions, services, devices, autonomous agents, and computational processes.

#### Identity

An Identity is the Runtime’s persistent representation of a Subject within a governing context.

An Identity provides continuity while its credentials, Claims, providers, relationships, and lifecycle state change. A Subject may possess more than one Identity where distinct realms, contexts, or purposes require separate representation.

#### Identifier

An Identifier is a value by which an Identity or Subject may be referenced within a defined scope.

Identifiers need not be globally unique. Their meaning and uniqueness derive from the context, authority, or scheme under which they are issued.

#### Principal

A Principal is an Identity as represented within an active operational context.

A Principal carries the authenticated and contextual information used by the Runtime when evaluating access or attributing an action. It may represent a Subject directly, act on behalf of another Identity, or operate under a delegated authority.

A Principal is therefore contextual and transient even where its underlying Identity is persistent.

#### Claim

A Claim is an assertion concerning an Identity, Subject, or Principal.

Claims are issued by an authority and interpreted within a Claim Set. Their value depends upon provenance, scope, validity, and the trust placed in their issuer. A Claim does not become true merely because it is presented.

#### Credential

A Credential is evidence presented through an authentication scheme to establish an association with an Identity.

Credentials may be issued, rotated, expired, suspended, compromised, or revoked without altering the continuity of the Identity to which they relate.

---

### §4. Authentication

Authentication evaluates presented credentials and evidence under an Authentication Scheme.

An Authentication Service produces an Authentication Result within an Authentication Context. A successful result establishes sufficient confidence, under the applicable scheme and policy, that a Principal may act in association with an Identity.

Authentication may create or update an Authentication Session. Sessions are bounded operational records and **SHALL NOT** be treated as Identities.

Multiple authentication schemes may establish access to the same Identity. Conversely, possession of a valid credential does not establish authority beyond the scope in which that credential is recognized.

---

### §5. Authorization

Authorization determines whether a Principal may perform an action within a particular context.

Permissions describe allowed capabilities. Roles may collect, imply, or contextualize Permissions. Authorization Services evaluate the Principal, relevant Claims, requested action, governing policy, and operational context.

Authorization decisions are contextual and may change without changing the underlying Identity.

Authentication and authorization are related but independent concerns. Successful authentication does not imply permission, and possession of permission does not itself authenticate a Principal.

---

### §6. Claims and Attestation

Identity Claims participate in the broader Knowledge model.

An Identity Claim is a Knowledge Claim concerning an Identity, Subject, or Principal. Its reliability depends upon the authority that issued or attested it, the evidence supporting it, its lifecycle state, and the trust relationships under which it is evaluated.

Attestation records an authority’s endorsement of a Claim. Revocation withdraws or invalidates that endorsement. Neither operation alters historical fact: the Runtime may retain that a Claim was issued, attested, relied upon, or revoked.

Claims **SHALL** be evaluated as governed assertions rather than unqualified facts.

---

### §7. Provisioning and Resolution

Identity Providers connect the Runtime to systems capable of supplying, managing, or authenticating Identities.

Identity Resolvers locate or correlate Identities from available identifiers, Claims, references, or provider-specific evidence. Resolution may establish correspondence between representations, but correspondence **SHALL NOT** be interpreted as equivalence without sufficient authority.

Provisioning creates or updates an Identity representation within a governing context. Provisioning **SHALL** preserve the distinction between:

- the Subject being represented;
- the Identity maintained by the Runtime;
- the external provider’s representation;
- and the evidence used to associate them.

No provider defines the ontology of Identity merely by storing or authenticating it.

External directory providers may supply current facts about identities and group membership independently of authentication. Directory references preserve the provider, realm, and stable external identifier that give those facts their scope. Directory observations identify when the facts were obtained and, when known, how long they may be treated as current.

A directory lookup that finds no subject or group is distinct from one that cannot establish an answer because its provider is unavailable, untrusted, or misconfigured. Consumers **SHALL NOT** interpret an unavailable or untrusted observation as authoritative absence. The application using a directory fact remains responsible for assigning domain meaning to external groups; the Identity domain does not infer application roles from group names or identifiers.

---

### §8. Lifecycle

Identity has an explicit and governable lifecycle.

An Identity State describes its current condition. Identity Lifecycle Policies determine which transitions are permitted, while Lifecycle Services apply those transitions and produce Lifecycle Events.

Lifecycle transitions may include creation, activation, suspension, restoration, retirement, revocation, or other domain-defined changes.

Lifecycle history is distinct from current state. A transition records change; it **SHALL NOT** erase the fact that an Identity previously existed or participated in the Runtime.

Termination of an Identity does not necessarily entail deletion of every Claim, attribution, event, or historical relationship concerning it.

---

### §9. Trust

Trust determines how Identity information originating outside a governing context is interpreted.

An Identity Issuer is an authority capable of issuing Identity representations, Claims, credentials, or attestations. An Identity Realm defines a context within which identifiers, issuers, schemes, and policies possess meaning.

A Trust Relationship describes the conditions under which one realm or authority accepts assertions made by another. Trust may be limited by:

- issuer;
- subject;
- Claim type;
- authentication scheme;
- assurance level;
- purpose;
- audience;
- time;
- or policy.

Trust is neither universal nor transitive by default.

Acceptance of an issuer does not require acceptance of every assertion that issuer may make.

---

### §10. Relationships

An Identity may:

- represent a Subject;
- possess one or more scoped Identifiers;
- be associated with multiple credentials and authentication schemes;
- participate through one or more Principals or sessions;
- possess Claims issued or attested by recognized authorities;
- be granted Roles and Permissions;
- belong to, represent, or act for one or more Institutions;
- author, issue, curate, or steward Knowledge;
- be resolved or provisioned through one or more providers;
- transition through governed lifecycle states;
- and participate in trust relationships across realms.

These relationships do not replace or redefine the Identity itself.

---

### §11. Principles

- Identity is persistent across ordinary changes of credential, session, Claim, provider, and authorization state.
- Identity is distinguishable within its governing context.
- Identity exists independently of any particular means by which it is authenticated.
- Identity may represent any Subject capable of recognition or participation within the Runtime.
- Identity state and Identity history are distinct.
- Identifiers are scoped and derive meaning from their issuing context.
- Claims are assertions whose authority and provenance must remain available for evaluation.
- Principals and sessions are contextual representations, not persistent Identities.
- Trust is explicit, bounded, and non-transitive unless policy states otherwise.
- External providers supply representations and evidence; they do not define Runtime identity semantics.

---

### §12. Invariants

Every Identity domain **SHALL** preserve the following invariants:

- An Identity **SHALL NOT** be defined by its credentials.
- Replacing or revoking a credential **SHALL NOT**, by itself, replace or revoke the associated Identity.
- Authentication **SHALL** establish confidence under a scheme and context; it **SHALL NOT** be treated as absolute proof.
- Authentication **SHALL NOT** imply authorization.
- Authorization **SHALL** govern permitted action; it **SHALL NOT** define the Identity being governed.
- A Principal **SHALL NOT** be treated as interchangeable with its underlying Identity.
- A session **SHALL NOT** be treated as the persistent record of an Identity.
- A Claim **SHALL** retain its issuer, context, and applicable lifecycle information.
- A Claim **SHALL NOT** be accepted solely because it is presented by an authenticated Principal.
- Identifiers from distinct realms **SHALL NOT** be presumed equivalent.
- Trust **SHALL NOT** be presumed universal or transitive.
- Suspension, retirement, or revocation **SHALL NOT** silently erase historical attribution.
- An Identity Provider **SHALL NOT** become the canonical definition of Identity merely because it stores or authenticates an external representation.
