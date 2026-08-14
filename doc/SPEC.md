# eShop Business Requirements Specification

This document lists the business requirements for the eShop demo platform. The team
adds to this document as the team gathers more requirements.

[`System landscape.md`](./System%20landscape.md) describes the system landscape that
comes from these requirements. The ADR files under [`doc/adr`](./adr) record the
architecture decisions that come from these requirements. [`TODO.md`](./TODO.md) lists
the open questions and the deferred questions.

## Scope

This platform serves the individual shopper. This platform does not serve the business
buyer. This platform does not serve the organization account. The list of actors does
not include a B2B actor or an organization buyer actor.

## Domain

The store is a movie store. The store sells movies. The store sells movie merchandise.
Sellers supply these products. See the [Seller](#seller) actor for more data about this
supply.

A movie is one catalog product. A movie has one or more purchasable formats. The system
calls a purchasable format a variant. The system calls the identifier of a variant a
SKU. The store sells these variants of a movie:

- A DVD copy.
- A Blu-ray copy.
- A streaming entitlement key. The streaming entitlement key gives the Shopper online
  access to watch the movie.

The store sells movie merchandise as a separate catalog product. Movie merchandise is
not a variant of a movie.

Movie merchandise can have one or more purchasable variants. For example, a T-shirt can
have a variant for each size.

A movie has a genre. A movie has a name. A movie has a year of release. A movie can have
an adult rating. The store must check the age of a Shopper before the store sells an
adult-rated movie to the Shopper.

More than one Seller can offer the same SKU. Each Seller sets their own price for a SKU
that the Seller offers. See the [Seller](#seller) actor for more data about this
marketplace design.

The Marketer runs campaigns with discounts. The Marketer creates a theme-based bundle.
The Marketer also creates an "often purchased with" bundle. The Marketer bases an "often
purchased with" bundle on an analysis of past customer purchases. The Marketer creates
each bundle by hand. The system gives the Marketer no automated analytics tool for this
task.

## Actors

The list that follows shows the actors of the system.

### Shopper

The Shopper looks at the catalog and the personalized content. The Shopper can search
the catalog by name. The Shopper can search the catalog by year of release. The Shopper
can browse the catalog by genre.

A Shopper does not need a profile to buy a product. The system calls a Shopper without a
profile an anonymous Shopper. An anonymous Shopper can do these tasks:

- Add a product variant to the cart from the product detail page of the variant.
- Start the checkout flow.

The checkout flow has these steps, in order:

1. A billing address step and a shipping address step.
2. An order summary step.
3. A payment step. The payment step lets the Shopper pick one configured commerce
   payment method. The payment step has a call to action to place the order.
4. An order confirmation step. The order confirmation step shows a call to action. The
   call to action lets the Shopper create a profile. The system links the new order to
   the new profile.

A Shopper can create a profile. The profile is optional. The profile lets the Shopper do
these tasks:

- Store the Shopper's name.
- Store the Shopper's billing address and the Shopper's shipping address.
- Store the Shopper's date of birth. The system uses the date of birth to check that the
  Shopper is old enough to buy an adult-rated movie.
- Keep a library of the movies that the Shopper owns.
- Rate each movie that the Shopper owns.
- Keep a list of "liked" SKUs.
- Record the consents that the Shopper accepts.
- Keep one or more wish lists.
- Keep one or more saved carts.
- Look at the Shopper's own purchase orders.
- Contact customer support.

The Shopper can contact customer support only through the profile. This rule gives the
anonymous Shopper a reason to create a profile.

### Seller

The Seller supplies the product information, the assets, and the price for the Seller's
own products. The Seller also supplies the vendor name. More than one Seller can offer
the same movie or the same piece of merchandise. Each Seller sets their own price for
the SKU that the Seller offers. This design gives the store competing offers on one
SKU, in the style of a marketplace.

The Seller uses a Seller Portal. The Seller does not use Optimizely Commerce Connect or
Commerce Manager. This rule stops a Seller from seeing the data of other Sellers. The
Seller Portal also gives a simple user interface. Sellers do not have experience with
complex user interfaces.

The Seller cannot see order data or fulfillment data.

A Site Administrator must approve a new Seller before the Seller can use the Seller
Portal.

#### Product data

The Seller submits this product data for a movie:

- The title.
- The genre.
- The description.
- The year of release.
- One cover image.
- One or more format variants that the Seller can fulfill, for example a DVD copy, a
  Blu-ray copy, or a streaming entitlement key. The Seller sets a price for each format
  variant.

The Seller submits this product data for a piece of merchandise:

- The product name.
- The description.
- One or more variants that the Seller can fulfill, for example a T-shirt size. The
  Seller sets a price for each variant.
- Optionally up to three product images.
- An optional movie association. The Seller identifies the associated movie by the
  movie title.

#### Submission workflow

The Seller submits product data through this workflow:

1. The Seller creates a draft product. The draft product can be a movie or a piece of
   merchandise.
2. The Seller edits the draft product until the product data is complete.
3. The Seller submits the completed product data.
4. Before the Merchandiser responds, the Seller can cancel the submission. A
   cancelled submission returns the product data to a draft. The Seller can edit the
   draft product data and restart the workflow from step 2.
5. The system starts an automatic deduplication process on the submitted product data.
   The deduplication process matches the submitted product data against the existing
   SKUs, for example by title, year of release, and format. The deduplication process
   assigns an existing SKU to the submission when the process finds a match. The
   deduplication process assigns a new SKU to the submission when the process finds no
   match.
6. The Merchandiser reviews the match from the deduplication process. The Merchandiser
   can approve the proposed match, pick a different existing SKU, mark the submission
   as a new SKU, or reject the submission with a reason. See the
   [Merchandiser](#merchandiser) actor for more data about this review.
7. When the Merchandiser rejects the submission, the system tells the Seller the
   rejection reason. The Seller can revise the draft product data and restart the
   workflow from step 2.
8. When the Merchandiser approves the submission, the system deletes the Seller's draft
   product data and saves the assigned SKU in the Seller's profile.

#### Inventory

The system models each Seller as a warehouse. After the Merchandiser approves an
assigned SKU, the Seller can report the inventory of that SKU. The system saves the
reported inventory into the Seller's own warehouse. The Seller reports inventory as a
task apart from the submission workflow. The Seller cannot see the inventory of another
Seller.

### Content Editor

The Content Editor can edit all content in the CMS.

The Content Editor must approve the marketing content from a Marketer before the system
publishes the marketing content.

### Marketer

The Marketer has limited rights to edit content in the CMS. A Content Editor must
approve a content change from a Marketer before the system publishes the content change.

The Marketer has full rights to manage campaigns and product discounts.

### Merchandiser

The Merchandiser owns the catalog structure, the categories, and the taxonomy.

The Merchandiser must review and approve the product data from a Seller before the
product data goes live. The Merchandiser reviews the SKU match that the automatic
deduplication process proposes. The Merchandiser can approve the proposed match, pick a
different existing SKU, mark the submission as a new SKU, or reject the submission with
a reason. See the [Seller](#seller) actor for more data about the submission workflow.

### Customer Service Agent

The Customer Service Agent can look at and manage the data of a Shopper on behalf of the
Shopper. The Customer Service Agent can issue refunds. The Customer Service Agent can use
the Order Manager.

The Customer Service Agent cannot manage a legally binding consent on behalf of a
Shopper.

### Site Administrator

The Site Administrator can use the Optimizely admin settings. The Site Administrator can
also use all other areas of the CMS user interface and the Commerce Connect user
interface.

The Site Administrator must approve a new Seller before the Seller can use the Seller
Portal.

Optimizely does not manage the users and the roles of the system. See
[System landscape](./System%20landscape.md#identity) for more data about user and role
management.

### Developer/Operator

The Developer/Operator runs, debugs, deploys, and monitors the system. The
Developer/Operator can use tools such as Aspire to do these tasks.

The Developer/Operator has the same access rights as a Site Administrator. The
Developer/Operator is not a separate access level. The list shows the
Developer/Operator as a separate actor because the needs of the Developer/Operator come
from development and operations tasks, not from business tasks.
