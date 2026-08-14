# 0001 - Single combined CMS and Commerce Connect site

## Status

Accepted

## Context

Optimizely Commerce Connect uses Optimizely CMS as its base. A Commerce Connect site
always includes the full CMS engine. The catalog entries of Commerce Connect, such as
categories, products, and variants, are content items. The system renders these content
items through the same pipeline as the marketing pages. Optimizely gives no supported
way to run Commerce Connect without CMS.

## Decision

The team builds the Optimizely site as one project. The team creates this project from
the `epi-commerce-empty` template. This project combines CMS and Commerce Connect in one
application with two databases. This site serves the Shopper. The Content Editor, the
Marketer, and the Merchandiser also work in this site.

## Consequences

- The team builds, deploys, and operates one project and two databases for CMS and
  Commerce Connect.
- The system needs no content federation between a separate CMS-only site and a
  commerce site.
- The team may need a separate editorial site in the future, for example a corporate
  site or a blog site apart from the store. That need is a separate multi-site decision.
  That need is not a CMS/Commerce Connect split. The team records that need in a new ADR.
