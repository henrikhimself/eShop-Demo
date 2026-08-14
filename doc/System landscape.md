# eShop System Landscape

This document describes the system landscape. The requirements in
[`SPEC.md`](./SPEC.md) give the source of this system landscape. The team adds to this
document as the team refines the architecture and the functionality.

The ADR files under [`doc/adr`](./adr) record the architecture decisions that come from
this system landscape.

## Identity

One external identity provider manages all identities of the system. The system
connects to the identity provider through OpenID Connect. The identity provider manages
the identity of the Shopper. The identity provider also manages the identity of each
internal user, such as the Content Editor, the Marketer, the Merchandiser, the Customer
Service Agent, and the Site Administrator. Optimizely CMS and Commerce Connect do not
manage users or roles.

The identity provider product is Keycloak for local development. See
[ADR 0010](./adr/0010-keycloak-identity-provider.md) for this decision. Aspire hosts a
Keycloak container for local development.

The local Keycloak setup creates development identities for each non-Shopper actor in
[`SPEC.md`](./SPEC.md). The Shopper stays unseeded until the Storefront needs Shopper
login.

A deployed instance uses Microsoft Entra External ID instead. See
[ADR 0020](./adr/0020-entra-external-id-production-identity-provider.md) for this
decision.

## Storefront (CMS + Commerce Connect)

One combined Optimizely CMS and Commerce Connect site serves the Shopper. The Content
Editor, the Marketer, and the Merchandiser also work in this site.

The Storefront needs two SQL Server databases. One database stores CMS data. One
database stores Commerce Connect data. See
[ADR 0001](./adr/0001-single-combined-cms-commerce-site.md) for this decision.

The team has not yet added these databases to the Aspire AppHost. The team adds them
when the team scaffolds the Storefront project.

The Storefront does not change database schema from normal application startup.
Explicit migration resources own schema changes for both databases. Services use
readiness checks until the required schema marker exists. See
[ADR 0023](./adr/0023-explicit-database-schema-migration-resources.md) for this
decision.

The Storefront catalog content model includes a movie product type and a movie variant
type. The variant represents a purchasable movie format.

The Storefront catalog content model includes a merchandise product type and a
merchandise variant type. The variant represents a purchasable merchandise option.

The Storefront uses a dedicated search microservice for catalog search. This
microservice owns the search backend integration. Elasticsearch is the local
development search engine. Azure AI Search is the managed search service for a deployed
instance. See [ADR 0022](./adr/0022-storefront-search-service.md) and
[ADR 0024](./adr/0024-search-microservice-boundary.md) for these decisions.

The first search microservice slice has a basic API and health checks only. The team
adds catalog indexing and query endpoints after it decides the search document shape.

The Storefront uses hidden submission content types for product review. The team has
not yet decided if each submission kind needs its own content type.

The team defers CMS content types for checkout until the team designs checkout.

## Profile microservice

The Profile microservice is a customer-centric microservice. The Profile microservice
stores the Shopper data that does not fit well in the Customer Management module of
Commerce Connect. The team has not yet decided the exact data split between Commerce
Connect and the Profile microservice. See
[ADR 0004](./adr/0004-profile-microservice-for-shopper-data-gaps.md) for more data about
this decision.

## Seller Portal

The Seller Portal is a separate application for the Seller. The Seller uses the Seller
Portal to supply the product information, the assets, the price, and the vendor name.
The system moves the product data from a Seller into the catalog of the Storefront only
after a Merchandiser approves the product data. See
[Seller Portal and Commerce Connect integration](#seller-portal-and-commerce-connect-integration)
for the mechanism that moves this data.

The Seller Portal has its own SQL Server database. Aspire hosts this database. This
database is separate from the data of Commerce Connect. See
[ADR 0003](./adr/0003-seller-portal-separate-application.md) for this isolation rule. A
deployed instance uses Azure SQL Database instead of a plain SQL Server container. See
[ADR 0019](./adr/0019-azure-sql-database-managed-sql-service.md) for this decision.

The Seller Portal also uses Redis. Redis stores the Seller's authentication ticket and
the Seller Portal's Data Protection key ring, so a Seller session survives a restart of
the Seller Portal backend-for-frontend. Aspire hosts this Redis instance for local
development; a deployed instance uses Azure Cache for Redis instead. See
[ADR 0021](./adr/0021-redis-replaces-valkey.md) for this decision.

The Seller Portal's backend-for-frontend also owns the data contracts between itself
and the Seller Portal frontend. The backend-for-frontend generates an OpenAPI document
of its own contracts. The frontend generates its own TypeScript types from this
document, instead of a Seller Portal developer defining the same contract twice. See
[ADR 0014](./adr/0014-bff-openapi-source-of-truth-for-frontend-types.md) for this
decision.

The Seller Portal frontend never calls the backend-for-frontend directly. The frontend
forwards every `/bff/*` request through its own server. This reverse proxy keeps the
browser on one origin. The proxy also carries the OIDC login and callback redirects and
the session cookie.

## DevTools

DevTools is a development-only application. It never appears in a deployed instance.

The Developer/Operator uses DevTools to inspect and drive development workflows. These
workflows can include actions that do not suit the Merchandiser user interface. They
can also include actions that CMS approval sequences cannot do.

DevTools uses the same message broker and object storage as the Seller Portal and the
Storefront. This lets DevTools help a developer test integration flows.

## Seller Portal and Commerce Connect integration

The Seller Portal and Commerce Connect exchange messages through a durable message
queue. The system uses Azure Service Bus as the message broker. Aspire hosts Azure
Service Bus and its local emulator. See
[ADR 0009](./adr/0009-durable-messaging-seller-portal-commerce-connect.md) for this
decision.

The Seller Portal sends a submission message to Commerce Connect when a Seller submits
product data. Commerce Connect creates a lightweight, hidden submission content item
from the message. The submission content item goes through the native Optimizely
content approval flow, so the Merchandiser can review the item in the Content Approvals
list. See
[ADR 0008](./adr/0008-submission-content-with-native-approval.md) for this decision.

Commerce Connect sends a review outcome message back to the Seller Portal after a
scheduled job processes an Approved or a Rejected submission. An approved outcome
message carries the assigned SKU. A rejected outcome message carries the rejection
reason and the movie title or the merchandise name and the format, so the Seller Portal
can match the outcome to the correct draft.

A submission with image data does not carry the image data in the message. The system
stores each image in Azure Blob Storage as a temporary file while the submission waits
for review, and the message carries only a reference to the stored image. See
[ADR 0009](./adr/0009-durable-messaging-seller-portal-commerce-connect.md) for this
decision.

## Deployment

The team deploys the eShop solution to Azure Container Apps. See
[ADR 0018](./adr/0018-azure-container-apps-deployment-target.md) for this decision.

Deployment starts database migration resources before dependent application revisions.
Dependent services still use readiness checks as a second gate. See
[ADR 0023](./adr/0023-explicit-database-schema-migration-resources.md) for this
decision.
