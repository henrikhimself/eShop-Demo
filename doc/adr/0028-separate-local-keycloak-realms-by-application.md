# 0028 - Separate local Keycloak realms by application

## Status

Accepted

Amends [ADR 0002](./0002-external-oidc-identity-provider.md) for local Keycloak
behavior only.

## Context

The Seller Portal has one Seller actor. The Storefront has the internal Storefront
actors. Local development used one Keycloak realm for both applications. This setup gave
both applications one issuer and one Keycloak browser session.

The team needs separate local identity boundaries for the Seller Portal and the
Storefront.

## Decision

The local Keycloak server has two realms. The `seller` realm contains the Seller Portal
client and Seller identities. The `storefront` realm contains the Storefront client and
Storefront internal identities.

The applications use their own realm issuer. The realms do not provide cross-application
single sign-on.

This decision does not change the production Microsoft Entra External ID provider.

## Consequences

- A Seller session does not authenticate a Storefront request.
- A Storefront user cannot authenticate through the Seller realm.
- The local Keycloak server and public identity host stay shared.
- A developer must remove an existing persistent Keycloak container before a clean realm
  import can remove the old shared realm.
