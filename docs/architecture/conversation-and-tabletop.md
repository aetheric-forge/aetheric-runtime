# Conversation and TableTop provider boundaries

These shared runtime contracts support GM's v0.4 session notebook and other hosts. They do not depend on GM, UI, storage, authentication middleware, Slack, or Owlbear SDKs. Interfaces live in Abstractions.Interfaces; reusable provider base classes and references live in Models.

## Conversation

`IConversationProvider` and abstract `ConversationProvider` expose cancellable history enumeration, plain-text message sending (optionally within a thread), and event subscriptions. A future `SlackConversationProvider : ConversationProvider` maps Slack concepts to these contracts. It owns authentication, formatting translation, pagination, rate limits, and webhook/socket delivery.

Messages retain their external ID, participant ID, thread ID, timestamps, and optional source URL. Conversation events distinguish creation, edits, and deletion; deletion events may omit the message body. Participant IDs are opaque external identities, not runtime user IDs or authorization decisions. The host is responsible for identity mapping and permission checks.

This is a human-conversation boundary. Post Office remains responsible for application message transport and routing. A host may publish normalized conversation events through Post Office; these contracts do not add another broker.

## TableTop

`ITableTopProvider` and abstract `TableTopProvider` expose room/table state, active-scene requests, and subscriptions to connection and scene events. A future `OwlbearRodeoTableTopProvider : TableTopProvider` owns event subscribers and API forwarding. Its browser-extension or host bridge architecture must be verified against the vendor SDK before implementation; no remote server API is assumed here.

This initial boundary does not include tokens, maps, initiative, combat rules, or arbitrary vendor payload forwarding. An adapter must report unsupported operations with `NotSupportedException`, never silently acknowledge them. Additional operations and capability discovery can be introduced with a concrete integration use case.

## Addressing and delivery

References contain a provider name, workspace/account scope, and opaque external ID. Names and IDs are compared ordinally and are not case-folded or trimmed. Provider base classes reject references belonging to another provider, missing address fields, and already-cancelled calls before dispatch. Adapters validate the remaining account scope, input limits, and authorization.

Subscription calls return an `IAsyncDisposable` lease. Adapters must detach callbacks on disposal and must release any resources acquired before a failed/cancelled subscription. Cancellation during setup must not leave a live subscription behind. Subsequent lifetime is controlled by the lease. Consumers must handle repeated events; delivery is not guaranteed to be ordered or exactly once.

Deduplicate using provider, scope, conversation/table ID, and event ID. Outgoing operations carry a caller-generated stable operation ID; incoming events may carry the originating operation ID where the adapter can recover it. Hosts must persist deduplication/origin state where needed. These fields enable loop prevention and retry reconciliation but do not themselves guarantee idempotency or prevent loops. Do not blindly retry a send after an ambiguous timeout.

History adapters own pagination and must document their ordering and enforce cancellation while enumerating. Plain-text messages are deliberately distinct from a notebook's Markdown; publishing a polished narrative needs an explicit rendering and user-authorized sharing operation.

## Scope of this change

Only abstractions, base validation, and tests are implemented. No external messages are sent; no Slack or Owlbear connection is registered. There are no placeholder adapters that pretend to succeed. GM's pinned runtime and institution registration remain unchanged until these contracts are reviewed and integrated.

Notebook/session IDs remain application-owned. Session-to-conversation/table associations belong in the application layer. Imported notebook content should preserve these external references and revision provenance rather than replacing the editable narrative automatically.
