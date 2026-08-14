# 0018 - Azure Container Apps as the deployment target

## Status

Accepted

## Context

The team has not yet decided where a deployed instance of the eShop solution runs.
The AppHost project defines only local development resources today. Aspire supports
more than one deployment target through its own hosting integrations, for example
Docker Compose, Azure Container Apps, and Azure Kubernetes Service.

The team wants a serverless container platform. The team wants native support from
Aspire's own tooling, with as little separate Bicep or Kubernetes work as possible.
Azure Container Apps meets both these wants. The `aspire deploy` command builds
container images, provisions the environment, and deploys each compute resource of
the AppHost.

## Decision

The team picks Azure Container Apps as the deployment target for the eShop solution.
The team adds the `Aspire.Hosting.Azure.AppContainers` integration to the AppHost
project when the team starts the deployment work. The team deploys with the
`aspire deploy` command.

## Consequences

- The AppHost project needs an Azure Container Apps environment resource, added
  through `AddAzureContainerAppEnvironment`, before a deployment can succeed.
- Azure Container Apps has no built-in way to delay one deployed container's start
  until another deployed container is ready. The AppHost's own `WaitFor` mechanism
  only orders resource startup during local development, through Aspire's own
  orchestrator. A deployed instance loses this ordering guarantee entirely. Each
  service must handle a not-yet-ready dependency on its own, through retry logic at
  startup and at first use, not through the AppHost's dependency graph. The team has
  not yet decided the exact retry mechanism for each service. `doc/TODO.md` tracks
  this open item.
- A backing service with no native Azure resource integration in the AppHost, for
  example Keycloak or Valkey, runs as an ordinary container inside the same Azure
  Container Apps environment as the rest of the solution. The team has not decided
  whether to keep this arrangement in a deployed environment, or to move to a managed
  Azure service instead. `doc/TODO.md` tracks this open item.
- The team can still change the exact scaling, ingress, and revision settings of a
  generated Container App, through `PublishAsAzureContainerApp`, without a new ADR.
