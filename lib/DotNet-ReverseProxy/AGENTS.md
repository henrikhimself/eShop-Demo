# Agent Instructions

## Project Overview
This repository is a .NET library that combines Microsoft YARP (Yet Another Reverse Proxy) with runtime configuration APIs and automatic self-signed ephemeral certificate generation. It's designed to simplify development and testing of reverse proxy scenarios. The `examples/` directory is set up to use Aspire. Aspire is an orchestrator for the entire application and will take care of configuring dependencies, building, and running the application. The resources that make up the application are defined in `examples/Aspire.AppHost/Program.cs` including application code and external dependencies.

The `src/ReverseProxy` directory implements the core functionality of the library. The `src/ReverseProxy.Aspire` directory is an extension that integrates `src/ReverseProxy` into an Aspire solution for managing YARP route/cluster configuration.

### Technology Stack
- .NET 10.0
- .NET 8.0
- Aspire
- C#
- Multi-platform support (Windows, Linux, macOS, containers)

## General
* Make only high confidence suggestions when reviewing code changes.
* Always use the version of C# that matches the latest .NET LTS.
* Always use the latest released (stable) version of Aspire.
* Never change global.json unless explicitly asked to.
* Never change package.json or package-lock.json files unless explicitly asked to.
* Never change NuGet.config files unless explicitly asked to.

## Formatting
* Apply code-formatting style defined in `.editorconfig` and `.editorconfig` files in nested directories.
* Prefer file-scoped namespace declarations and single-line using directives.
* Insert a newline before the opening curly brace of any code block (e.g., after `if`, `for`, `while`, `foreach`, `using`, `try`, etc.).
* Ensure that the final return statement of a method is on its own line.
* Use pattern matching and switch expressions wherever possible.
* Use `nameof` instead of string literals when referring to member names.
* Place private class declarations at the bottom of the file.

### Nullable Reference Types
* Declare variables non-nullable, and check for `null` at entry points.
* Always use `is null` or `is not null` instead of `== null` or `!= null`.
* Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.

## Markdown files
* Markdown files should not have multiple consecutive blank lines.
* Code blocks should be formatted with triple backticks (```) and include the language identifier for syntax highlighting.
* JSON code blocks should be indented properly.

## Available Skills
The following specialized skills are available in `.github/skills/`:
- **aspire**: Aspire skill covering the Aspire CLI, AppHost orchestration, service discovery, integrations, MCP server, VS Code extension, Dev Containers, templates, dashboard, and deployment

## Project Layout and Architecture

### Key Components
1. **ReverseProxy Core** (`src/ReverseProxy`)
   - **ReverseProxyApp**: Manages routes/clusters using YARP's configuration system
   - **Certificate Management**: Automatic generation of self-signed ephemeral certificates with caching and trust added by a local CA
   - **Runtime API**: Web API endpoints for managing routes/clusters at runtime

2. **ReverseProxy.Aspire** (`src/ReverseProxy.Aspire`)
   - Integrates with the Aspire orchestration platform for route/cluster configuration for services without requiring the **Runtime API**
   - `WithReverseProxyReference()`: Extension for configuring Aspire resources
   - `ServiceDiscoveryStartupFilter`: Auto-discovers and configures annotated Aspire resources as YARP routes/clusters

3. **Examples** (`examples/`)
   - AppHost: Aspire app host demonstrating the reverse proxy in action
   - ReverseProxy: Example reverse proxy service using the library
   - Website: Example backend service discoverable by the proxy

### Configuration Layering
- **InMemoryConfigProvider**: Holds runtime-added routes/clusters
- **IProxyConfigProvider**: Base abstraction; implementations are merged in ReverseProxyApp
- Multiple providers can coexist; routes/clusters are combined across all

### Dependency Injection Pattern
Services use primary constructor injection with sealed internal classes:
```csharp
internal sealed class ReverseProxyApp(
  IConfigValidator configValidator,
  InMemoryConfigProvider inMemoryConfigProvider,
  IEnumerable<IProxyConfigProvider> proxyConfigProviders)
```

Use the extension methods in `src/ReverseProxy/ReverseProxyExtensions.cs` to configure services:
- `ConfigureReverseProxy()`: Registers core services
- `UseSelfSignedCertificate()`: Enables automatic certificate generation on Kestrel

### Certificate Management
- **Lazy Generation**: Certificates are created on-demand via ServerCertificateSelector
- **Caching**: IMemoryCache prevents redundant certificate generation
- **Wildcard Support**: Handles both `*.example.com` and specific domain names
- **Self-Signed CA**: Generated once and installed into the trusted root store on the platform; certificates derive trust from it

## Testing Strategy

### Test Framework & Patterns
- **xUnit** with **SutFactory** for mocking
- **SutFactory** (HenrikJensen.SutFactory): Custom builder pattern for automatic arrangement of the dependency graph while creating the System Under Test instance
  - Reduces boilerplate; use `SutBuilder` → `InputBuilder` → `Instance<T>()` or if no spy instances are inspected, the preferred pattern `SystemUnderTest.For<T>(arrange => { })` where 'arrange' is an InputBuilder.
- **Arrange-Act-Assert**: Standard pattern with setup helpers (see `SetHappyPath(InputBuilder arrange)` in tests)

### Key Test Files
- `test/ReverseProxy.UnitTest/ReverseProxy/ReverseProxyApiTests.cs`: Tests the API endpoints
- `test/ReverseProxy.UnitTest/ReverseProxy/ReverseProxyAppTests.cs`: Tests configuration management
- `test/ReverseProxy.UnitTest/Certificate/CertificateAppTests.cs`: Tests certificate generation
- `test/ReverseProxy.UnitTest/Certificate/CertificateConfigTests.cs`: Tests configuration management
- `test/ReverseProxy.Aspire.UnitTest/ServiceDiscoveryTests.cs`: Tests the Aspire service discovery

### Running Tests
To test the library run the following command from the solution root:

```
dotnet test
```

## Build & Development

### Multi-Targeting
Projects target both `net8.0` and `net10.0`. Compatibility with Windows, macOS and Linux must be ensured when adding and changing code.
Use dotnet test to verify.

### Code Analysis & Style
- **EnforceCodeStyleInBuild**: Code style violations fail the build
- Configuration: `stylecop.json` requires `xmlHeader: true` for packable projects

### Package Management
Central package versioning via `Directory.Packages.props`:
- Add new dependencies by adding `<PackageVersion>` entries there
- Projects reference by `<PackageReference Include="Name" />` (version auto-inherited)

### Global Usings
Common namespaces are globally imported via `Directory.Build.props` and project `.csproj` files

## Integration Points

### YARP Integration
- Uses `Yarp.ReverseProxy` NuGet package
- Core types: `RouteConfig`, `ClusterConfig`, `DestinationConfig`
- Validation via `IConfigValidator`

### Aspire Integration
- DI service registration via `IServiceCollection`.
- Startup filters for configuring reverse proxy routes/clusters using injected service discovery environment variables.
- Resource builders follow Aspire conventions

### Extension Points
- **IProxyConfigProvider**: Implement to add custom configuration sources
- **ICertificateConfig**: Customize certificate generation options (algorithm, subject name)
- **IFileStore**: Abstract file storage for certificates

## Trust These Instructions
These instructions are comprehensive and tested. Only search for additional information if:
1. The instructions appear outdated or incorrect
2. You encounter specific errors not covered here
3. You need details about new features not yet documented
