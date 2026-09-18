# Architecture Decision Records

This directory holds the Architecture Decision Records (ADRs) of the eShop platform.
Each ADR records one architecture decision. Each ADR gives the context of the decision
and the result of the decision.

## Convention

- Each file name has the format `NNNN-short-title.md`. `NNNN` is a sequence number with
  leading zeros.
- Each ADR has these sections: Status, Context, Decision, Consequences.
- The team does not delete an ADR after the team accepts the ADR. If the team reverses a
  decision, the team adds a new ADR. The new ADR supersedes the old ADR. The team then
  updates the Status of the old ADR.

## Index

- [0001 - Single combined CMS and Commerce Connect site](./0001-single-combined-cms-commerce-site.md)
- [0002 - External OpenID Connect identity provider for all identities](./0002-external-oidc-identity-provider.md)
- [0003 - Seller Portal as a separate application](./0003-seller-portal-separate-application.md)
- [0004 - Profile microservice for shopper data gaps](./0004-profile-microservice-for-shopper-data-gaps.md)
- [0005 - Movie catalog modeled with product variants](./0005-movie-catalog-product-variants.md)
- [0006 - Marketplace competing offers on a shared SKU](./0006-marketplace-competing-offers-shared-sku.md)
- [0007 - Seller-reported inventory through a per-seller warehouse](./0007-seller-reported-inventory-per-warehouse.md)
- [0008 - Submission content type reviewed through native content approval](./0008-submission-content-with-native-approval.md)
- [0009 - Durable messaging between the Seller Portal and Commerce Connect](./0009-durable-messaging-seller-portal-commerce-connect.md)
- [0010 - Keycloak as the identity provider product](./0010-keycloak-identity-provider.md)
- [0011 - Playwright as the browser end-to-end testing tool](./0011-playwright-for-browser-e2e-tests.md)
- [0012 - Valkey for the Seller Portal's auth ticket store and Data Protection key ring](./0012-valkey-for-ticket-store-and-data-protection.md)
- [0013 - Pinned dependency versions, updated only with a 7-day quarantine](./0013-pinned-dependency-versions-and-7-day-quarantine.md)
- [0014 - The Bff's OpenAPI document as the source of truth for frontend types](./0014-bff-openapi-source-of-truth-for-frontend-types.md)
- [0015 - SignalR for live updates on the Seller Draft Approval Simulator page](./0015-signalr-for-devtools-live-updates.md)
- [0016 - EShop.Cli replaces the Bash developer scripts](./0016-eshop-cli-replaces-bash-scripts.md)
- [0017 - EShop.Cli supports Linux only, for now](./0017-linux-only-eshop-cli-for-now.md)
- [0018 - Azure Container Apps as the deployment target](./0018-azure-container-apps-deployment-target.md)
- [0019 - Azure SQL Database as the managed production SQL service](./0019-azure-sql-database-managed-sql-service.md)
- [0020 - Microsoft Entra External ID as the production identity provider](./0020-entra-external-id-production-identity-provider.md)
- [0021 - Redis, not Valkey, for the cache and Data Protection key ring](./0021-redis-replaces-valkey.md)
- [0022 - Storefront search service](./0022-storefront-search-service.md)
- [0023 - Explicit database schema migration resources](./0023-explicit-database-schema-migration-resources.md)
- [0024 - Search microservice boundary](./0024-search-microservice-boundary.md)
- [0025 - Local-development reverse proxy for stable browser hosts](./0025-local-development-reverse-proxy.md)
