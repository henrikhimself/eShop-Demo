# 0005 - Movie catalog modeled with product variants

## Status

Accepted

## Context

The store sells a movie in more than one purchasable format: a DVD copy, a Blu-ray copy,
and a streaming entitlement key. The store also sells movie merchandise. Movie
merchandise is not a format of a movie. Optimizely Commerce Connect gives a native
catalog model for this exact case. In this model, a product can have one or more
variants. A variant is a purchasable format of the product.

## Decision

The team models a movie as a Commerce Connect product. The team models the DVD copy, the
Blu-ray copy, and the streaming entitlement key as variants of this product. The team
models each piece of movie merchandise as a separate Commerce Connect product. Movie
merchandise has no variant relationship to a movie.

## Consequences

- The catalog uses the native product/variant model of Commerce Connect for a movie and
  its formats. The team does not need a custom data model for this relationship.
- The store shows one product page for a movie. This product page lets the Shopper pick
  a variant, for example the DVD copy or the streaming entitlement key.
- The store shows a separate product page for each piece of movie merchandise.
- A Seller supplies the product data, the variant data, and the price for a movie and
  for movie merchandise. See
  [ADR 0003](./0003-seller-portal-separate-application.md) for the process that brings
  this data into the catalog.
