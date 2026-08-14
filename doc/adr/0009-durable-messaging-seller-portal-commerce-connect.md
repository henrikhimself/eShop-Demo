# 0009 - Durable messaging between the Seller Portal and Commerce Connect

## Status

Accepted

## Context

The Seller Portal and Commerce Connect are separate applications (see
[ADR 0003](./0003-seller-portal-separate-application.md)). The product submission
workflow crosses this boundary twice: the Seller Portal sends a submission to Commerce
Connect, and Commerce Connect later sends the review outcome back to the Seller Portal.
The Merchandiser reviews a submission at a time of the Merchandiser's choosing (see
[ADR 0008](./0008-submission-content-with-native-approval.md)). The wait for a review
can be long. Either application can also be down for a short time, for example during a
deployment.

A Seller's submission can include binary image data: one cover image for a movie, or up
to three images for a piece of merchandise. A durable message queue has a limit on
message size. Azure Service Bus limits a message to 256 KB on the Standard tier and to
1 MB on the Premium tier. An image, or a set of up to three images, can pass this limit.

## Decision

The system uses a durable message queue for both directions of the integration: the
Seller Portal sends a submission message to Commerce Connect, and Commerce Connect
sends a review outcome message back to the Seller Portal. The durable queue keeps a
message until the receiving application reads it, so neither the wait for a review nor
a short outage causes the system to lose a message.

The system uses Azure Service Bus as the message broker. Aspire hosts Azure Service Bus
and its local emulator, so the team can develop and test the integration without an
Azure subscription. Aspire can also provision a real Azure Service Bus namespace for a
deployed environment.

The system stores each image in Azure Blob Storage as a temporary file while a
submission waits for review. Aspire hosts Azure Blob Storage and its local emulator. A
submission message and a review outcome message carry only the product data and a
reference to the stored image, not the image data itself. This pattern is a claim
check: the message is a claim on the data held apart from the message.

The system deletes a temporary image file once the submission reaches its final state,
Rejected or Published.

## Consequences

- The Seller Portal, Commerce Connect, and the scheduled jobs in
  [ADR 0008](./0008-submission-content-with-native-approval.md) need a client for Azure
  Service Bus and a client for Azure Blob Storage.
- A message stays small, regardless of the size or the number of images in a
  submission.
- The system needs a step that deletes a temporary image file once a submission reaches
  its final state.
- The exact message payload shape and the exact queue and topic names are
  implementation details and are not decided here.
