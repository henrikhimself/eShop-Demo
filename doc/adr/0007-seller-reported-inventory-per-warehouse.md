# 0007 - Seller-reported inventory through a per-seller warehouse

## Status

Accepted

This ADR amends [ADR 0003](./0003-seller-portal-separate-application.md). ADR 0003
states that the Seller has no visibility into inventory. This ADR replaces that
statement with the decision that follows.

## Context

The store lets more than one Seller offer against one SKU (see
[ADR 0006](./0006-marketplace-competing-offers-shared-sku.md)). Each Seller needs a way
to tell the store how many units of an approved SKU the Seller can supply. The store
must keep the inventory of one Seller apart from the inventory of another Seller.

## Decision

The system models each Seller as a warehouse. After the Merchandiser approves an
assigned SKU, the Seller can report the inventory of that SKU into the Seller's own
warehouse. The Seller reports inventory as a task apart from the product submission
workflow. The Seller cannot see the inventory of another Seller's warehouse.

## Consequences

- Commerce Connect needs one warehouse per Seller.
- The Seller Portal needs a feature that lets the Seller report and update the
  inventory of an approved SKU, apart from the submission workflow.
- The Seller still has no access to Commerce Connect or Commerce Manager. The Seller
  reports inventory only through the Seller Portal.
- The Seller still has no visibility into order data or fulfillment data. This part of
  ADR 0003 stays in effect.
