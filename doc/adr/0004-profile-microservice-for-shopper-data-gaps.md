# 0004 - Profile microservice for shopper data gaps

## Status

Accepted

## Context

A Shopper can create an optional profile. The profile can store the Shopper's name and
date of birth for age verification. The profile can store a list of "liked" SKUs. The
profile can store the consents that the Shopper accepts. The profile can store one or
more wish lists. The profile can store one or more saved carts. The profile can show the
purchase orders of the Shopper. The Customer Management module of Optimizely Commerce
Connect already gives core customer data through its Business Foundation. The goal of
the README is that "customer-centric microservices" own customer-related data. These
microservices must add to Commerce Connect. These microservices must not copy Commerce
Connect.

## Decision

The team uses the built-in Customer Management module of Commerce Connect for the
customer data that this module supports well. The team builds a separate Profile
microservice. This microservice owns the Shopper data and the Shopper tasks that do not
fit well in the customer model and the order model of Commerce Connect.

The team has not yet decided the exact data split between Commerce Connect and the
Profile microservice, field by field. The team defers this decision. The team makes this
decision only when the team discusses concrete functionality. This method lets the team
base the decision on the real gaps in Commerce Connect. This method stops the team from
a decision too early.

## Consequences

- Two systems own Shopper data together: Commerce Connect owns the native customer data
  and the native order data. The Profile microservice owns the gaps.
- The team needs a future decision about the exact data split. The team also needs a
  future decision about the read access and the write access between the two systems,
  for example to show the saved carts or the order history next to the profile data in
  the Storefront. `doc/TODO.md` tracks these open items.
