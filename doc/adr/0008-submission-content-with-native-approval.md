# 0008 - Submission content type reviewed through native content approval

## Status

Accepted

## Context

The Merchandiser must review the SKU match from the automatic deduplication process
before the store publishes a Seller's product data (see
[ADR 0006](./0006-marketplace-competing-offers-shared-sku.md)). The store needs a
review screen for the Merchandiser. Optimizely CMS has a built-in approval system for
content. The approval system moves a content item through the states CheckedOut,
AwaitingApproval, Rejected or CheckedIn, and Published.

## Decision

The system defines a separate, lightweight content type for a submission. The
submission content type implements `IVersionable`, so the native approval system can
manage the submission through the same states. The submission content type does not
appear in the CMS or Commerce Connect editor tree. The Merchandiser reviews a
submission through the Content Approvals list, not through the editor tree.

A submission holds the Seller's submitted product data and the proposed SKU match from
the automatic deduplication process. The Merchandiser can approve the proposed match,
pick a different existing SKU, mark the submission as a new SKU, or reject the
submission with a reason.

The system creates a new submission content item for every submission from a Seller,
including a resubmission after a rejection. The Seller Portal does not track or reuse
the identifier of a submission content item.

A scheduled job processes each Rejected submission. The job places a rejection message
on the message queue (see
[ADR 0009](./0009-durable-messaging-seller-portal-commerce-connect.md)). The rejection
message carries the movie title or the merchandise name and the format, so the Seller
Portal can match the rejection to the correct draft. The job then deletes the
submission content item.

A second scheduled job processes each Approved submission. The job creates a new
product and variant, or resolves the submission to the existing variant that the
Merchandiser confirmed. The job then places a message on the message queue to report
the assigned SKU. The job then deletes the submission content item.

A submission content item never stays in the system after the system processes it. The
system does not need a bucketing scheme for submission content, because the number of
open submissions stays small.

## Consequences

- The system needs a lightweight, hidden content type for a submission, separate from
  the live catalog product and variant content types.
- The Merchandiser uses the built-in Content Approvals list to review a submission, not
  a custom screen.
- The system needs two scheduled jobs: one job to clean up a Rejected submission and
  report the rejection, and one job to publish an Approved submission to the catalog and
  report the assigned SKU.
- The live catalog never holds a placeholder product or a placeholder variant for a
  submission that turns out to match an existing SKU.
- A Seller who resubmits after a rejection restarts the deduplication and the review
  process from the start, using the revised product data.
