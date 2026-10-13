# Provisioned institutions and consumer configuration

The worker now keeps an institution catalog under `Provisioning:DataDirectory/institutions`.
It records both individual deployments and the four levels of bootstrap through the same
plan/execute pipeline. A record contains its definition and bindings provenance, version,
environment, parent deployment identity, executed resource settings, generated secret references,
and the latest outcome. Secret values and root credentials are not stored in this catalog.

The stable deployment ID hashes the definition repository, institution ID, and environment.
Changing a display name, definition version, commit, or request ID does not create a second
institution. Existing resource providers still name accounts from the institution/resource IDs;
the catalog does not create an additional tenant-isolation layer on shared infrastructure.

Snapshots are private, atomic files, with an operation lease serializing execution and consumer
delivery for each institution. A failed or pending update retains the last successfully provisioned
revision. Superseded requests cannot replace a newer attempt. A completed checkpoint cannot be
exported against a changed backing endpoint without a new reviewed plan.

## Inventory query for admin

The trusted provisioning broker accepts `institution-catalog-requested` version `1.0` at
`provisioning/institution/catalog` (Request intent). The payload is:

```json
{
  "RequestId": "<request UUID>",
  "Environment": "production",
  "ParentDeploymentId": null
}
```

The correlated response is `institution-catalog-returned` version `1.0` at
`provisioning/institution/catalog/result` (Response intent). Its `Institutions` array includes
`DeploymentId`, `LatestAttempt`, and nullable `Provisioned`. Admin should build its institution
inventory from `Provisioned`, and show `LatestAttempt` separately when an update is pending or
failed. A valid definition or a successful preflight is never counted as a provisioned institution.
Environment and parent filters use the provisioned revision when one exists.

An individual deployment request can select its parent by setting `Parent.DeploymentId`.
The worker resolves the nearest owning ancestors from that parent's provisioned containment
chain, ignoring caller-supplied locations in this mode. Missing parents, missing ancestors,
cycles, and cross-environment containment fail closed. The existing live capability checks
still run before resource provisioning. Legacy requests without a deployment ID continue to
use their explicit, live-verifiable parent locations.

Bootstrap records containment even for composition-only levels with no capability dependencies.
Existing completed installations do not acquire catalog records merely by upgrading the worker.
Replay their existing reviewed bootstrap/deployment requests using the same source/bindings and
saved connections: the normal checkpoint path reuses completed resources and secret references.
Do not reconstruct a supposedly provisioned inventory from appsettings or sample definitions.

## Parallel You consumer

Set `Provisioning:ParallelYou:PublicOrigin` to the web host's exact HTTPS origin (HTTP loopback
is allowed for development). A successful `parallel-you` institution deployment then:

1. Uses the Library's generated Mongo credential and its actual bound database.
2. Creates/reconciles a separate Redis ACL user, restricted to the data and lock keys of
   ReflectionMapping, TrackingCurrent, IntentionCurrent, PlanCurrent, and RecommendationCurrent.
   It has the staging client's required hash/expiry/transaction/lock commands; it cannot access
   other namespaces or flush the database. The Redis server must have a writable persistent
   `aclfile`; successful `ACL SAVE` is required before the export is published.
3. Creates a browser-only confidential OIDC client in the verified parent Registry realm.
   It uses authorization-code flow and S256 PKCE, exact `/signin-oidc` and
   `/signout-callback-oidc` URLs, and no service-account or realm-management roles. An existing
   client with a different provisioning owner is never adopted or overwritten.
4. Publishes one complete versioned consumer configuration. It includes the institution's
   deployment ID, name, version, environment and plan ID, and only its consumer settings.

Application credentials are generated once through the existing encrypted secret store and
reused on retry. The root Mongo, Redis and Keycloak credentials stay in the provisioning request;
they do not enter the consumer artifact. An export failure does not undo completed resources:
the catalog records `Consumer.Status=Failed`, and the command completion reports a safe issue
code. Retry reuses completed resources and reconciles the consumer grants. A missing public
origin reports `consumer.public_origin_required` rather than claiming usable delivery.

`Consumer.Status=Ready` means configuration delivery completed. It does not assert that the web
deployment has restarted or that its runtime is healthy. Retain the worker's catalog, run state,
encrypted secrets and encryption key across upgrades.

## Delivery destinations

By default, a private atomic file is written at
`<DataDirectory>/consumers/<DeploymentId>/configuration.json`. For Kubernetes, set
`Provisioning:ConsumerSecret:Namespace` and `Provisioning:ConsumerSecret:Name`. The worker uses
its projected service-account token (refreshed on each request) and the cluster CA to update
that one pre-created Secret instead. The artifact is stored under `configuration.json`.

The Kubernetes adapter requires `get` and `patch` access to that named Secret only. It refuses
to overwrite a Secret owned by another deployment or an unclaimed Secret containing existing
data, and uses its resourceVersion to avoid lost updates. See
`examples/parallel-you-consumer-rbac.yaml` for a target Secret, service account, and narrow Role.
Set the worker Deployment's `serviceAccountName` to that account when enabling this destination.
The example is not applied automatically.

Mount only this consumer Secret in the web host and configure:

```text
ParallelYou__ProvisionedConfigurationPath=/var/run/parallel-you/configuration.json
ParallelYou__DeploymentId=<ID returned by the institution catalog>
ParallelYou__Environment=production
PublicSite__Enabled=true
PublicSite__ProtectionKeyDirectory=<web host's persistent key directory>
```

The web host consumes the complete artifact before registering authentication and module services.
It rejects a wrong deployment/environment, missing or extra settings, invalid JSON, and missing
files. It does not read the worker catalog, root stores, or provisioning encryption key. An
optional `ParallelYou:PlanId` pins the consumer revision as well. Restart the web host after a
configuration update; the connection and OIDC options are not hot-reloaded.

Deployment, inventory selection, and any migration of existing personal artifacts remain explicit
operator actions. In particular, old captures/person records may live in the former ForgeCampus
knowledge database. Do not switch an existing person to a new Library without inspecting and
migrating their records and Person IDs.
