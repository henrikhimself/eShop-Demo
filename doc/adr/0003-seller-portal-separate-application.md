# 0003 - Seller Portal as a separate application

## Status

Accepted

[ADR 0007](./0007-seller-reported-inventory-per-warehouse.md) amends the statement in
this ADR that the Seller has no visibility into inventory.

## Context

The marketplace model needs each Seller to supply product information, assets, price,
and vendor name for the Seller's own products. Optimizely Commerce Manager is the normal
place to manage catalog data. Commerce Manager gives a user visibility into the data of
all Sellers. This visibility is not acceptable for the Seller actor. In addition, the
Seller does not have experience with a complex administrative user interface such as
Commerce Manager.

## Decision

The team builds a separate Seller Portal application for the Seller. This application is
apart from Optimizely Commerce Connect and Commerce Manager. This application gives the
Seller a narrow set of tasks: to manage the product information, the assets, the price,
and the vendor name of the Seller's own products only. The Seller has no access to
Commerce Connect. The Seller has no visibility into inventory. The Seller has no
visibility into orders or fulfillment.

A Merchandiser must review and approve the product data from the Seller Portal before
the product data goes live in the catalog of the Storefront. A Site Administrator must
approve and onboard each new Seller.

## Consequences

- The Seller Portal is a separate application. The Seller Portal has its own scoped data
  access. This scope isolates the data of one Seller from the data of other Sellers and
  from the wider administrative surface of Commerce Connect.
- The system needs an integration method and an approval method to move the product data
  of a Seller into the catalog of Commerce Connect after approval. The team has not yet
  decided this method. `doc/TODO.md` tracks this open item.
- Commerce Connect keeps sole ownership of the inventory data and the order data. The
  Seller Portal does not need to integrate with these systems.
