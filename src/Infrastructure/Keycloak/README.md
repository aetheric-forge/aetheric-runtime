# Keycloak IAM Setup

This directory contains a basic Docker Compose setup for Keycloak.

## Getting Started

1. Start Keycloak:
   ```bash
   docker-compose up -d
   ```
2. Access the Admin Console at [http://localhost:8080](http://localhost:8080).
3. Log in with `admin`/`admin`.

## Configuration for Aetheric Runtime

To use Keycloak with the Aetheric Runtime Identity Provider, you should:

1. Create a new Realm (e.g., `Aetheric`).
2. Create a new Client (e.g., `runtime-api`).
   - Client Protocol: `openid-connect`.
   - Access Type: `confidential` (or `public` if using PKCE).
   - Valid Redirect URIs: `*` (for development).
3. Obtain the Client Secret from the "Credentials" tab.

## Options Configuration

Configure the `KeycloakOptions` in your application:

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/Aetheric",
    "ClientId": "runtime-api",
    "ClientSecret": "your-client-secret",
    "Realm": "Aetheric"
  }
}
```

The realm service account used for runtime directory reads requires Keycloak Admin API
permissions to view users and groups (normally the `view-users` role from the
`realm-management` client). The provider obtains a client-credentials token and exposes
directory reads through `KeycloakExternalIdentityDirectory`:

```csharp
IExternalIdentityDirectory directory =
    new KeycloakExternalIdentityDirectory(httpClient, keycloakOptions);
```

`Authority` should be the realm authority (for example,
`https://keycloak.example/realms/campus`). The Admin API address is derived from it.
Set `AdminApiBaseAddress` explicitly when Keycloak is exposed through a proxy whose
Admin API does not share the authority's base path. Successful observations are fresh
for one minute by default; use `DirectoryFreshnessLifetime` to change that policy.
