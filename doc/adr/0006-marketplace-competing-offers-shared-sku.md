# 0006 - Marketplace competing offers on a shared SKU

## Status

Accepted

## Context

More than one Seller can supply the same movie or the same piece of merchandise. Each
Seller submits full product data through the Seller Portal. The store needs one catalog
entry for a movie or a piece of merchandise, not one catalog entry per Seller. The store
also needs to let each Seller set their own price for the item that the Seller supplies.

## Decision

The store gives each purchasable format of a product one SKU. The system lets more than
one Seller supply an offer on one SKU. Each offer has its own price. The store shows the
competing offers on one SKU to the Shopper, in the style of a marketplace.

The system finds the shared SKU for a submission through an automatic deduplication
process. The Seller submits a draft product with full product data. The system compares
the submitted product data against the existing SKUs, for example by title, year of
release, and format. The system proposes an existing SKU when the process finds a match.
The system proposes a new SKU when the process finds no match. The Merchandiser reviews
the proposed SKU and approves the product data for publishing.

## Consequences

- The catalog needs a data model that lets more than one Seller offer against one SKU,
  each with a separate price.
- The store must decide how it presents more than one offer on one SKU to the Shopper,
  for example a list of offers or a single best-price offer. This decision is deferred;
  `doc/TODO.md` tracks it.
- The automatic deduplication process needs matching logic and a review step for the
  Merchandiser. The exact matching logic is an implementation detail and is not decided
  here.
