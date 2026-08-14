# 0002 - External OpenID Connect identity provider for all identities

## Status

Accepted

[ADR 0010](./0010-keycloak-identity-provider.md) amends this ADR. ADR 0010 names
Keycloak as the identity provider product.

## Context

The system has external identities (the Shopper) and internal identities (the Content
Editor, the Marketer, the Merchandiser, the Customer Service Agent, and the Site
Administrator). Each internal actor needs access to more than one application: the
Optimizely CMS and Commerce Connect site, the Seller Portal, and the Profile
microservice. Optimizely CMS and Commerce Connect include a built-in identity
mechanism, for example ASP.NET Core Identity. This built-in mechanism ties all user and
role management to Optimizely. This built-in mechanism does not extend well to other
applications in the system, such as the Seller Portal.

## Decision

The team uses one external identity provider for all identities. The system connects to
this identity provider through OpenID Connect. The Shopper and each internal user get
their identity from this provider. The Optimizely CMS and Commerce Connect site, the
Seller Portal, and each microservice trust this external identity provider. Optimizely
CMS and Commerce Connect do not manage users or roles on their own.

## Consequences

- The system manages users and roles in one place: the external identity provider.
- Each application in the system, for example the Storefront, the Seller Portal, and the
  Profile microservice, uses the same identity provider. This design gives one sign-on
  process for the internal user across applications.
- The system does not use the built-in membership system of Optimizely as the source of
  truth. The team must configure Optimizely to trust the tokens and the claims from the
  external identity provider.
- The team has not yet chosen the specific identity provider product. The team records
  that choice in a separate decision.
