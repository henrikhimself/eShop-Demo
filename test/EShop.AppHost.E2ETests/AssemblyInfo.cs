using Xunit;

// Each test here starts its own full Aspire stack (SQL Server, Service Bus/Storage
// emulators, Keycloak) via a real Docker daemon. Running two concurrently starves
// each other's containers and causes spurious startup failures.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
