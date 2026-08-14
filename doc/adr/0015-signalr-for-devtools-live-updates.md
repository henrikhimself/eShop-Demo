# 0015 - SignalR for live updates on the Seller Draft Approval Simulator page

## Status

Accepted

## Context

The Seller Draft Approval Simulator page in `src/EShop.DevTools` shows the list of
pending submissions. Today a developer must reload the page by hand to see a new
submission arrive. A developer must also reload by hand to see that a submission was
approved or rejected in another open tab. The page has no push mechanism.

`src/EShop.DevTools` is dev-only tooling. It never appears in a real deployment. See
`src/EShop.AppHost/AppHost.cs`'s publish-mode check. It runs as a plain Aspire project
resource with its own direct HTTP endpoint. No reverse proxy sits in front of it.

The Seller Portal Bff faced a similar push need earlier. The team chose Server-Sent
Events over SignalR for that case. The Bff sits behind a Next.js reverse proxy. That
proxy cannot terminate a WebSocket upgrade without dropping a build mode the deployment
pipeline needs. See `doc/CHRONICLE.md` for that decision.

## Decision

The team adds SignalR to `src/EShop.DevTools`. The server side uses the
`Microsoft.AspNetCore.SignalR` APIs already in the shared framework this project
references. The team also adds a locally vendored `@microsoft/signalr` browser client,
at `src/EShop.DevTools/wwwroot/lib/signalr/signalr.min.js`, pinned per ADR 0013.

A new push-only `SubmissionsHub` broadcasts one "submissionsChanged" event. The
broadcast fires whenever the in-memory `PendingSubmissionStore` gains a submission or
loses one. Every open page reloads itself in full on receipt. This is the same reload
the page already does after a Seller submits, or after a developer approves or rejects,
through the existing POST-redirect-GET pattern. The team adds no partial update and no
client-side re-rendering.

This decision does not contradict the Seller Portal's earlier choice. That earlier
choice was about the Bff's proxy topology. It was not a rejection of SignalR as a
technology. `src/EShop.DevTools` has no reverse proxy in front of it. The reason the Bff
rejected SignalR does not apply here.

The team pins a test-only `Microsoft.AspNetCore.SignalR.Client` package in
`Directory.Packages.props`, per ADR 0013. `test/EShop.DevTools.Tests` uses this package
to connect a real client and confirm the broadcast reaches it.

## Consequences

- `src/EShop.DevTools` now enables `app.UseStaticFiles()`. It can serve the vendored
  SignalR browser script and any future static asset this project adds.
- The team commits a third-party minified JavaScript file to the repository. A future
  version bump goes through the same pinned-version, 40-day quarantine review as every
  other dependency, per ADR 0013.
- A developer testing this tool locally now sees the pending submission list update on
  its own, in any open tab, with no manual reload needed.
