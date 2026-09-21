# Existing Workbench parent capability

The single-institution `institution-deployment-requested` v2 worker now verifies inherited `IWorkbench` through the existing Redis workspace ownership registration. The message shape is unchanged: this is an additional `Parent.ResourceLocations` contract entry and a `redis` root credential entry.

`ResourceLocations["IWorkbench"]` is a JSON string containing:

```json
{"Version":1,"Environment":"production","Institution":"owning-faculty","Resource":"workspace","Stage":"adr-campus-workbench"}
```

These identify the **resource-owning ancestor**, which may differ from the immediate parent represented by `Parent.Repository` and `Parent.Revision`. The deployment bindings still declare a logical parent source such as `IWorkbench: { source: owner.workbench }`. The resolver checks that the requested source matches `ParentContext.Capabilities`.

Credentials use `redis` with Host, Port, optional Username, Password, Scheme (`redis` or `rediss`, default `redis`), and Database (nonnegative Redis database index encoded as a string, default `0`). Connections are lazy and request-scoped; pure preflight performs no Redis I/O. The server ACL must allow reading the registration through the read-only Lua script (`PTTL`, `GET`, and script invocation). TLS uses the configured endpoint hostname and normal certificate verification.

The check atomically reads a nonexpiring registration at `RedisWorkbenchBackend.RegistrationKey(Stage)` and verifies Version=1, Format=`runtime-staging-hash-v1`, Environment, Institution, Resource and Stage. Missing, malformed, expiring or mismatched registrations are unavailable. The resolver never creates/adopts a stage or writes/deletes draft keys. It verifies provisioning ownership, not the application user's read/write permissions; those still require host readiness checks after mounting.

No legacy adoption is implicit. An existing populated but unregistered stage remains unavailable. Back up and explicitly reconcile historical ownership in a separately reviewed migration; do not run workspace creation against populated legacy data or change its name to bypass the check.

Missing/invalid location or credential configuration yields a failed parent check (`parent:IWorkbench`, `parent.unavailable`). Connection/authentication/ACL failures are sanitized by the engine as `operation.failed`; raw connection exceptions and credentials do not enter public outcomes. Unknown contract requests do not trigger Redis I/O. Cancellation is checked around connection and read operations, with bounded connection timeout.

`ILibrary` retains its existing semantics: ResourceLocations contains the owning institution ID, and Mongo verifies its `{id}-library` scoped user in `admin`. It does not take `database@id` (the old message comment was stale), nor does it prove the mounted app's access to a specific database. Root Mongo credentials remain necessary for this existing check.

This change covers verification of an existing owner for single-institution deployment. It does not register Redis as an owned-resource provider in the worker, infer Workbench locations for a newly created bootstrap hierarchy, or mount a package.

Validation: 25 focused tests passed without skips, covering `WorkbenchParentCapabilityTests`, `WorkbenchTests`, `MongoDbLibraryParentCapabilityResolverTests`, and `CompositeParentCapabilityResolverTests`. Live tests used isolated Redis (database 2) and MongoDB fixtures. The companion ADR Campus suite passed all 248 tests.
