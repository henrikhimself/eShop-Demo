# 0014 - The Bff's OpenAPI document as the source of truth for frontend types

## Status

Accepted

## Context

The Seller Portal Bff and the Seller Portal Web frontend each define the same data
contracts on their own side. The Bff defines them as C# records. The frontend defines
them again, by hand, as TypeScript types. Nothing keeps the two definitions in sync. A
change to one side can drift from the other side without a build failure.

An OpenAPI document is a language-neutral description of an HTTP API. ASP.NET Core can
generate this document from the Bff's own Minimal API endpoints and C# types, with no
hand-written duplicate. A code generator can then turn this document into TypeScript
types for the frontend.

## Decision

The team makes the Bff's OpenAPI document the one source of truth for the data
contracts. The frontend no longer defines these contracts by hand.

The Bff generates its OpenAPI document at build time, through
`Microsoft.AspNetCore.OpenApi` and `Microsoft.Extensions.ApiDescription.Server`. Build-time
generation needs no running Aspire session, unlike scraping a live OpenAPI endpoint.

The frontend generates its own TypeScript types from this document, through
`openapi-typescript`. The team commits the generated TypeScript file to the repository,
the same way the team commits other generated code, for example an Entity Framework
Core migration. A build check regenerates the file into a temporary path. The check
compares the temporary file against the committed file. The check fails the build when
the two files differ.

## Consequences

- `src/EShop.SellerPortal.Web/lib/types.ts` no longer defines these contracts by hand.
  It re-exports the types a code generator writes into a second, generated file, so
  every existing import of `lib/types.ts` keeps working unchanged.
- A new top-level script builds the Bff. The script then runs the TypeScript code
  generator and writes the generated file. A developer runs this script after a change
  to a Bff contract or enum, and commits the result.
- The build report fails when the committed generated file does not match a fresh run
  of the code generator.
- The Bff's build-time OpenAPI document generation runs the application's own startup
  code through a mock host, with no real Aspire-provided database, message broker, or
  identity provider connection available. The Bff's own startup code skips every
  registration that needs one of these, during this kind of generation only.
