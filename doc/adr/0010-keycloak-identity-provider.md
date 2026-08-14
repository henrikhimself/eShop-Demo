# 0010 - Keycloak as the identity provider product

## Status

Accepted

[ADR 0020](./0020-entra-external-id-production-identity-provider.md) amends this ADR.
ADR 0020 names Microsoft Entra External ID as the identity provider for a deployed
instance. This ADR stays the decision for local development.

## Context

[ADR 0002](./0002-external-oidc-identity-provider.md) decides on one external OpenID Connect
identity provider for all identities. ADR 0002 does not pick a product. ADR 0002 defers
that choice to a separate decision.

The Storefront, the Seller Portal, and the Profile microservice need this identity
provider during local development. Aspire can host many products as local containers.
Aspire has an official hosting integration for Keycloak.

## Decision

The team picks Keycloak as the identity provider product. Keycloak is open source. A team
can self-host Keycloak. The eShop AppHost hosts a Keycloak container for local
development, through the `Aspire.Hosting.Keycloak` package.

## Consequences

- The eShop AppHost defines a Keycloak resource. This resource gives every application a
  real identity provider during local development, with no external account.
- A deployed environment needs its own Keycloak instance. The team has not yet decided
  the exact deployment topology for a production Keycloak instance.
- Each application configures its OpenID Connect client to trust this Keycloak instance.
  The team makes this configuration when the team builds each application.
