# 0026 - Server-Sent Events for Seller Portal notifications

## Status

Accepted

## Context

The Seller Portal must notify an open Seller Portal page when a submission or inventory
outcome changes. The Seller Portal frontend proxies browser requests to the backend-for-
frontend. This proxy cannot pass a WebSocket upgrade while the frontend uses the
standalone build mode required by the deployment pipeline.

## Decision

The Seller Portal backend-for-frontend uses Server-Sent Events for notification fan-out.
It keeps subscriber connections in one in-memory process. The frontend reloads
authoritative data after it receives an event.

## Consequences

- A notification has no replay and no cross-instance fan-out.
- A Seller with no open connection does not receive a notification.
- The normal Seller Portal data endpoints remain authoritative.
- A multi-instance notification design needs a new decision.
