using Xunit.Sdk;
using Xunit.v3;

// Each test here starts its own full Aspire stackvia a real Docker daemon.
// Running two concurrently starves each other's containers and causes
// spurious startup failures.
[assembly: Parallelization(Mode = ParallelMode.None)]
