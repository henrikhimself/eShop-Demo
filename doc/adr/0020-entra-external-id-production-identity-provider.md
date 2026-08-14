# 0020 - Microsoft Entra External ID as the production identity provider

## Status

Accepted

Amends [ADR 0010](./0010-keycloak-identity-provider.md). ADR 0010 stays the identity
provider decision for local development. This ADR adds the production decision ADR
0010 left open.

## Context

ADR 0018 picked Azure Container Apps as the deployment target. ADR 0010 picked
Keycloak as the identity provider product, but left the exact deployment topology for
a production Keycloak instance undecided. Aspire has no hosting integration for
Microsoft Entra External ID; a real Keycloak instance would need its own separate
deployment and operation effort the team has not planned for.

## Decision

The team picks Microsoft Entra External ID as the identity provider for a deployed
instance. Keycloak stays the identity provider for local development only. The Seller
Portal Bff reads a configuration value the AppHost sets, to pick the correct provider
at startup.

The team creates the Entra tenant, the app registration, and the user flow by hand, in
the Microsoft Entra admin center. The AppHost only holds the three values a deployed
instance needs at runtime: the tenant subdomain, the client ID, and the client secret.
Each value is a parameter with no default; `aspire deploy` asks for each value at
deploy time.

The Site Administrator onboards a new Seller through the Microsoft Entra admin
center's "Invite external user" flow. The Site Administrator assigns the Seller App
Role before sending the invite. This satisfies
[ADR 0003](./0003-seller-portal-separate-application.md)'s rule that a Site
Administrator must approve a new Seller before the Seller can use the Seller Portal.

## Consequences

- A deployed instance needs no Keycloak container, realm import, or dynamic client
  provisioning - all local-development-only concerns under ADR 0010.
- The Seller Portal Bff's OpenID Connect setup now picks between two providers at
  startup, each with its own authority, client credential, and role-claim mapping.
  Microsoft Entra External ID surfaces a Seller's assigned App Role as a `roles` claim,
  not the `role` claim Keycloak's realm role uses.
- The team has not written down the exact tenant/app-registration/user-flow setup
  steps anywhere. `doc/TODO.md` tracks this as an open item.
- This ADR does not decide who besides a Seller gets an Entra identity, or how. Other
  actor roles (Content Editor, Marketer, Merchandiser, Customer Service Agent, Site
  Administrator) are out of scope: none of them has a working implementation in code
  yet.
