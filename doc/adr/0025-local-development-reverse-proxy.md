# 0025 - Local-development reverse proxy for stable browser hosts

## Status

Accepted

## Context

Local development ran Seller Portal Web, the Storefront, and Keycloak on `localhost`,
each on its own Aspire-assigned port. Browser cookies scope to a host, not a host and
port pair. A cookie from one application on `localhost` was reachable by another
application on `localhost`, only on a different port. This caused a real failure: a
Seller Portal logout request accumulated unrelated `localhost` cookies from other
applications and failed.

Optimizely's paid license also needs a `.local` top-level domain for local development,
not `localhost`.

[ADR 0010](./0010-keycloak-identity-provider.md) picks Keycloak as the local
identity provider. Keycloak's local OpenID Connect clients used to need a
redirect URL for each application's own, dynamically assigned `localhost` port.
The AppHost provisioned each client at every local startup, after Aspire assigned
that port.

## Decision

The team adds one local-development-only reverse proxy in front of Seller Portal Web,
the Storefront, and Keycloak. The reverse proxy terminates browser HTTPS on one fixed
port. The reverse proxy gives each application a stable `*.local` browser host name,
routed by host name to that application's own internal HTTP endpoint.

Because every browser host name is now fixed, Keycloak's local OpenID Connect clients
for Seller Portal Web and the Storefront also become static realm configuration,
imported from a checked-in realm file, not provisioned at each local startup.

This decision does not change a deployed instance. [ADR 0018](./0018-azure-container-apps-deployment-target.md)'s
Azure Container Apps ingress stays the only production browser edge.

## Consequences

- A developer needs a one-time local machine setup: host file entries for the three
  `*.local` names, and trust for the reverse proxy's own local certificate authority.
  `DEVELOP.md` documents both steps.
- Seller Portal Web and the Storefront no longer expose a direct local browser
  endpoint. The reverse proxy is the only local browser ingress for these two
  applications and for Keycloak.
- Keycloak's local Seller Portal and Storefront OpenID Connect clients are now static,
  checked-in realm configuration. The AppHost no longer provisions them at startup.
- `doc/System landscape.md` ("Local development ingress") describes the exact browser
  host names, the fixed port, and the routing behavior. This ADR does not repeat that
  detail.
