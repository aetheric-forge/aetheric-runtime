# Provisioned web module handoff

Library, Archive, and Decisions use the existing `institution/deploy` request and
configure/review/approve/execute pipeline. A successful resource deployment is followed
by consumer delivery; delivery failure is recorded in the catalog and the request
result reports failure. Retrying the same plan reuses its scoped credentials.

The hosted source repositories are `aetheric-forge/library`, `aetheric-forge/archive`,
and `aetheric-forge/adr-campus` (Decisions). Use production bindings for production
deployment; the generated Decisions bindings in ADR Campus are an example environment.

The worker routes each module to an individual, pre-created Kubernetes Secret configured
with `Provisioning:Consumers:{Library|Archive|Decisions}:SecretName`. It can only patch
those named Secrets; it cannot create arbitrary Secrets. The route checks the repository,
module, deployment identity, and `Provisioning:Consumers:Environment` before delivery.
Without Secret targets, artifacts are stored in the worker's private consumer directory.

Library receives its existing MongoDB resource's scoped user and secret. Archive uses
SeaweedFS's embedded IAM API on its S3 endpoint. The adapter creates a tagged managed
identity, a bucket-only IAM policy, and an access key supplied from the encrypted secret
store. It never exports the S3 administrative credential. IAM writes must be enabled
with `-s3.iam.readOnly=false`; filer storage must persist IAM metadata. Existing unowned
identities and conflicting access keys are rejected. No MinIO administration client is
used. The legacy `MinIO` archive reference store name remains a data compatibility key.

Decisions requires a catalog-backed parent in the same environment. Its owner chain must
supply Library, Archive, Workbench, Post Office, and Registry resources. The request may
include `DecisionsConsumer` with `OrganizationId`, `DisplayName`, `MemberGroupId`, and
`MaintainerGroupId`; these identify the existing organization and membership groups.
Alternatively configure the worker's `Provisioning:Consumers:Decisions:Organization`
section with `Id`, `DisplayName`, `MemberGroupId`, and `MaintainerGroupId`.
Missing membership configuration, missing owner resources, and invalid ancestor chains
fail delivery. Provisioning creates a service-account directory client with only
`view-users`, `query-users`, and `query-groups`; this client cannot perform browser login
or manage users. It uses `Decisions:Directory:Keycloak`, separate from shell sign-in.

The web shell reads the mounted directory from `Provisioning:Consumers:Directory`.
Empty slots leave modules disabled. Each present artifact must be complete, match the
canonical deployment identity and environment, and contain only its profile's allowed
settings. Decisions additionally requires full campus hosting and an independently
configured `Decisions:ExpectedOwnerDeploymentId` matching the selected owner. The campus
continues supplying its existing Workbench settings; Decisions does not create a new
sovereign institution or adopt an unrelated workspace. A changed projected Secret causes
a graceful web-host restart so the whole revision is applied together.

Deployment of this wiring does not itself submit institution provisioning requests,
change production module flags, or migrate data. Operators provision the modules through
the service when selecting their resource bindings and, for Decisions, their owner.
