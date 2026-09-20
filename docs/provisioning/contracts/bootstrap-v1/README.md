# Institution bootstrap v1: admin interoperability profile

This profile fixes the existing `InstitutionBootstrapRequested` / `InstitutionBootstrapCompleted` JSON payloads and adds supplemental Post identity attributes. It does not change the v1 routing or require a shared compiled contract package. Existing v1 producers without the attributes remain compatible. New admin submissions must follow this profile. Only University → Campus → Administration Faculty → Decisions is supported; Talent is excluded.

## Transport and serialization

| | Request | Completion |
| --- | --- | --- |
| Domain | `provisioning` | `provisioning` |
| Address | `institution/deploy/bootstrap` | `institution/deploy/bootstrap/result` |
| Contract | `institution-bootstrap-requested` | `institution-bootstrap-completed` |
| Version | `1.0` | `1.0` |
| Intent | `Command` | `Event` |

Exchange: `aetheric.post.provisioning`. Routing keys are `{Address}.{Contract}.{Version}.{Intent}` lowercased. The broker body is **only Payload**, serialized with default `System.Text.Json`: PascalCase properties, explicit nulls, numeric enums, UTF-8. Fixture `Reference` and `Metadata` are test wrappers, not extra body fields. Post metadata is transmitted through AMQP properties/headers. `ProducedAtUtc` is second precision on the current AMQP transport; timestamps in the payload retain their JSON precision. Qualifiers are empty.

The canonical request and completion JSON files in this directory are consumed by independent tests in runtime and admin. Their institution text is deliberately a wire-format example, not a deployable Decisions or other institution definition. All credential values are synthetic. Changing a fixture requires testing both repositories; breaking payload/routing changes require a new contract version.

## Identity and causation

GUID strings in this profile use lowercase `D` format. Envelope and four institution request IDs must be nonempty and distinct.

| Meaning | Location |
| --- | --- |
| Approved envelope/submission ID | Payload `RequestId` and request Post `MessageId` |
| Initiating University request ID | Request/result Post `CorrelationId`; `bootstrap.university-request-id` |
| Campus request ID | `bootstrap.campus-request-id` |
| Administration Faculty request ID | `bootstrap.administration-faculty-request-id` |
| Decisions request ID | `bootstrap.decisions-request-id` |
| Logical priority | `bootstrap.priority`, always `Standard` |

The request has no Post `CausationId`: it originates in an operator action. University has no parent or preceding request. Campus's parent/prerequisite is University; Faculty's is Campus; Decisions' is Faculty. **All three child requests are caused by University**, independent of their immediate parent. These relationships are implied by the fixed four-slot topology and the named request IDs; the payload does not accept an arbitrary graph.

Completion payload `RequestId` identifies the original envelope. Completion Post `MessageId` is a fresh event ID, `CorrelationId` is copied from the incoming envelope (legacy fallback: incoming MessageId), and `CausationId` is the incoming envelope's MessageId. Only the five attributes above are copied into completion metadata. Match step labels to the corresponding request-ID attribute; do not infer child causation from step order. Headers are caller-supplied correlation data, not authentication or authorization evidence.

`Standard` is a logical job classification, not an implemented RabbitMQ priority or an Operations scheduling guarantee. Stable IDs are the intended retry identity, but this PR does not implement deduplication. Later delivery work must persist the approved submission, reuse its IDs/content for retries, reject an ID reused with different content, and give a changed approved submission a new envelope ID. Draft institution IDs may remain stable across edits.

## Approved configuration and credentials

Each of the four `InstitutionConfig` slots contains exact `DefinitionYaml` and `BindingsYaml` strings. They are resolved configuration snapshots, never paths or mutable profile references. Approval must cover the exact four pairs and their identity mapping; editing any resolved input invalidates approval. Runtime must not refetch a profile under the same approval. Creation of those snapshots and whole-hierarchy approval validation are subsequent work.

`RootCredentials` is a case-sensitive map of root infrastructure credentials; its values contain `Host`, `Port`, `Username`, `Password`, `AuthDatabase`, `Database`, `Scheme`, `BasePath`, and `Realm`. Optional fields serialize as null. This profile uses the existing inline credential transport; credentials must be resolved server-side and excluded from downloadable drafts, status records and logs. Any durable message storage in later delivery work must protect this secret-bearing payload.

| Provider | Credential key | Interpretation |
| --- | --- | --- |
| RabbitMQ | `rabbitmq` | Management endpoint; Scheme/BasePath, not the AMQP publishing connection |
| MongoDB | `mongo` | AuthDatabase; null uses runtime's `admin` default |
| Keycloak | `keycloak` | Root admin Username/Password; Realm is the **authentication** realm (default `master`), not the realm to create; provider currently uses `admin-cli` |
| S3 | `s3` | Username is access-key ID, Password is secret key; Scheme selects endpoint transport |

Only resolved owned-resource bindings determine required credentials; inherited resources do not automatically require another credential entry, though their live capability resolver may require Keycloak credentials. Missing required credentials must block submission. `redis` and `postgres` draft store keys are not automatically copied: this Worker does not yet construct a Workbench provider or a Postgres resource provider. A binding needing an unavailable provider must be blocked. Nondefault options absent from this payload (e.g. Mongo DirectConnection, Keycloak ClientId, S3 Region/ForcePathStyle) must not be silently discarded; block that configuration pending an explicit contract extension.

## Completion semantics

Exactly four results appear, in order: `University`, `Campus`, `AdministrationFaculty`, `Decisions`. Numeric status values are `Succeeded = 0`, `Failed = 1`, `NotAttempted = 2`. After a reported failure, remaining steps are NotAttempted. `Succeeded` is true only if all four succeeded. Earlier success is not rolled back. `Issues` contains public diagnostic strings, not credentials or raw provider exceptions. `CompletedAtUtc` is the completion timestamp; it is not a broker acknowledgement.

A completion event represents execution outcome, not durable acceptance. Runtime exceptions can currently interrupt completion publication. Persistent secret lifecycle, durable publish/acceptance, deduplication, bounded retries, durable results, and whole-envelope preflight remain separate work. Admin submission stays disabled until those integrations are complete.

## Verification

Run `dotnet test src/Tests/Aetheric.Provisioning.Tests/Aetheric.Provisioning.Tests.csproj --configuration Release --filter "FullyQualifiedName~BootstrapContractTests|FullyQualifiedName~BootstrapCompletionMetadataTests|FullyQualifiedName~InstitutionBootstrapResolverTests|FullyQualifiedName~InlineSourceDocumentsTests"` from the runtime root. All 13 tests passed for this change without broker/provider fixtures. Admin runs the same JSON fixtures against its independent types in its own test suite.
