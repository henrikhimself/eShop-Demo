# 0019 - Azure SQL Database as the managed production SQL service

## Status

Accepted

## Context

ADR 0018 picked Azure Container Apps as the deployment target. ADR 0018 left open
whether a backing service without a managed Azure equivalent stays a plain container in
a deployed instance. SQL Server has a managed Azure equivalent: Azure SQL Database.
Aspire has a hosting integration for Azure SQL Database, through the
`Aspire.Hosting.Azure.Sql` package.

## Decision

The team picks Azure SQL Database as the SQL service for a deployed instance. The
AppHost adds this service through `AddAzureSqlServer`. The AppHost still runs a local
SQL Server container during local development, through the same resource's
`RunAsContainer` method. This one resource covers both cases; the team makes no other
code change for this decision.

## Consequences

- Local development keeps the same SQL Server container as before this decision. A
  deployed instance gets a real Azure SQL Database instead of a plain container inside
  the Azure Container Apps environment.
- The Seller Portal Bff needs no code change. The Bff already connects through
  `Aspire.Microsoft.EntityFrameworkCore.SqlServer`, a client package that works
  unchanged against both SQL Server and Azure SQL Database.
- The team has not decided the exact tier, backup policy, or network topology (private
  endpoint, firewall rule) for the deployed database. Aspire's own default provisioning
  settings apply until the team decides otherwise.
