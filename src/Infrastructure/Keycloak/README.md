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
