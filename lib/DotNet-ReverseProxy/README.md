# ReverseProxy

[Features](#features) • [Quick Start](#quick-start) • [Usage](#usage) • [Examples](#examples) • [API Reference](#api-reference)

A .NET library that combines Microsoft YARP (Yet Another Reverse Proxy) with runtime configuration APIs and automatic self-signed certificate generation. Designed to simplify development and testing of reverse proxy scenarios with minimal setup.

## Features

- **Developer Ready** - Built on YARP's reverse proxy engine
- **Automatic HTTPS** - Self-signed certificates generated on-demand with local CA trust for seamless HTTPS testing
- **Aspire Integration** - Support for Aspire service discovery
- **Runtime Configuration** - Add and update routes and clusters dynamically via REST API
- **Wildcard Support** - Supports both specific domains and wildcard certificates
- **Automatic Caching** - Certificates are cached to avoid regeneration
- **Multi-Platform** - Works on Windows, macOS (including .NET 8 compatibility), and Linux

## Prerequisites

- Supported .NET SDK LTS
- Aspire (if using Aspire integration)

## Quick Start

### Installation

Install the NuGet package in your project:

```bash
dotnet add package HenrikJensen.ReverseProxy
```

For Aspire integration, also install:

```bash
dotnet add package HenrikJensen.ReverseProxy.Aspire
```

### Basic Setup

**1. Configure services in `Program.cs`:**

```csharp
// Enable automatic self-signed certificates signed by local CA
builder.WebHost.ConfigureKestrel(options =>
{
    options.UseSelfSignedCertificate();
});

// Add reverse proxy services
builder.Services.ConfigureReverseProxy(builder.Configuration);

// Use service discovery to set up routes and clusters for Aspire resources (optional)
builder.Services.AddTransient<IStartupFilter, ServiceDiscoveryStartupFilter>();

// Map the reverse proxy
app.UseReverseProxy();

// Map the runtime configuration API (optional)
app.UseReverseProxyApi();
```

**2. Start your application:**

```bash
dotnet run
```

The reverse proxy is now running with automatic HTTPS certificate generation and Aspire service discovery!

### Configuration

Certificate generation options are loaded from the `SelfSignedCertificate` configuration section.

```json
{
  "SelfSignedCertificate": {
    "CaFilePath": "{REVERSEPROXY_HOME}",
    "CaName": "ReverseProxy-RootCA",
    "AlgorithmOid": "1.2.840.10045.2.1",
    "SubjectName": "CN=ReverseProxy Root CA"
  }
}
```

#### SelfSignedCertificate options

| Key | Required | Default | Description |
| --- | --- | --- | --- |
| `CaFilePath` | Yes | None | Directory where the CA files are read/written. |
| `CaName` | No | `ReverseProxy-RootCA` | Base name used for generated CA files. |
| `AlgorithmOid` | No | `1.2.840.10045.2.1` (ECDSA) | Key algorithm OID used for CA and leaf certificate creation. |
| `SubjectName` | No | `CN=ReverseProxy Root CA` | Subject name for the generated CA certificate. |

#### Environment variable support

If `CaFilePath` contains `{REVERSEPROXY_HOME}`, the value is replaced with the `REVERSEPROXY_HOME` environment variable at runtime.

```bash
export REVERSEPROXY_HOME="$HOME/.reverseproxy"
```

Example:

```json
{
  "SelfSignedCertificate": {
    "CaFilePath": "{REVERSEPROXY_HOME}/certs"
  }
}
```

#### Algorithm OIDs

- `1.2.840.10045.2.1` (ECDSA, default)
- `1.2.840.113549.1.1.1` (RSA)

#### Files generated in `CaFilePath`

Using `CaName = ReverseProxy-RootCA`, the following files are generated:

- `ReverseProxy-RootCA.crt.pem`
- `ReverseProxy-RootCA.key.pem`
- `ReverseProxy-RootCA.pfx`

## Usage

### Aspire Integration

Use the Aspire integration to automatically configure routes based on service discovery:

**In your AppHost project:**

```csharp
using Hj.ReverseProxy.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

// Add backend resources. The resource name becomes the service-discovery name.
var websiteOne = builder.AddProject<Projects.MyWebsite>("website-one")
    .WithHttpEndpoint();
var websiteTwo = builder.AddProject<Projects.MyWebsite>("website-two")
    .WithHttpEndpoint();

// Add one reverse proxy HTTPS listener. Use 8443 when 443 is not available.
var reverseProxy = builder
    .AddProject<Projects.ReverseProxy>("reverse-proxy")
    .WithHttpsEndpoint(port: 8443);

// Configure fixed public hosts that route to internal HTTP endpoints.
reverseProxy.WithReverseProxyReference(
    endpointReference: websiteOne.GetEndpoint("http"),
    hostName: "one.eshop.local",
    forwardPublicOrigin: true);
reverseProxy.WithReverseProxyReference(
    endpointReference: websiteTwo.GetEndpoint("http"),
    hostName: "two.eshop.local",
    forwardPublicOrigin: true);

await builder.Build().RunAsync();
```

**Access your services:**
```
https://one.eshop.local:8443
https://two.eshop.local:8443
```

The reverse proxy automatically:
- Generates a self-signed certificate for each configured hostname
- Discovers each website endpoint from Aspire
- Routes public HTTPS traffic to backend HTTP resource services

Map each configured hostname to `127.0.0.1` and `::1` in your hosts file. The package
does not modify hosts files or install CA trust. Store the generated CA outside source
control, retain it between runs, and explicitly install it in the system trust store.

### Forwarded public origin

Set `forwardPublicOrigin: true` only for a target that needs the public HTTPS origin for
absolute URLs or OIDC redirects. The proxy removes client-provided `X-Forwarded-For`,
`X-Forwarded-Host`, and `X-Forwarded-Proto` values. It then sets only
`X-Forwarded-Host` and `X-Forwarded-Proto` from the received request. It does not
forward a client IP address and does not preserve the original public `Host` as the
downstream origin contract.

For example, an ASP.NET Core target can consume the proxy-created origin before it
creates redirects or absolute URLs:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto,
});
```

Do not expose that target directly to untrusted clients when it relies on these headers.
A target that does not need the public origin must pass `forwardPublicOrigin: false`.

### Version 2.0.0 migration

`WithReverseProxyReference` no longer accepts a free-form service name. Pass the target
endpoint reference instead. The extension now derives the service-discovery name from
that endpoint's resource. Add the required `forwardPublicOrigin` argument and choose
`true` only for targets that consume the public origin.

### Runtime Configuration via API

Add routes and clusters dynamically using the REST API:

**Add a route:**
```bash
curl -X POST http://localhost:5000/route \
  -H "Content-Type: application/json" \
  -d '{
    "routes": [{
      "routeId": "api-route",
      "clusterId": "api-cluster",
      "match": {
        "path": "/api/{**catch-all}"
      }
    }]
  }'
```

**Add a cluster:**
```bash
curl -X POST http://localhost:5000/cluster \
  -H "Content-Type: application/json" \
  -d '{
    "clusters": [{
      "clusterId": "api-cluster",
      "destinations": {
        "destination1": {
          "address": "https://catfact.ninja/fact"
        }
      }
    }]
  }'
```

**Get current configuration:**
```bash
# List all routes
curl http://localhost:5000/route

# List all clusters
curl http://localhost:5000/cluster
```

## Examples

The `examples` directory contains a complete example demonstrating:
- Two websites using HTTPS and distinct custom host names through one proxy listener
- Proxy-created forwarded public-origin headers
- Configuring YARP routes/clusters via
    - Aspire service discovery integration
    - Appsettings.json file
    - Runtime REST API
- An Aspire AppHost configuration
- A ReverseProxyApi.http file for calling the Runtime REST API

**Run the example:**
- The AppHost stores its generated CA under `~/.reverseproxy`, outside the repository
- Install the generated `ReverseProxy-RootCA` (PEM or PFX) into your system's trusted root store
- Add hosts-file entries that map `one.eshop.local` and `two.eshop.local` to `127.0.0.1` and `::1`

```bash
aspire start --apphost examples/Aspire.AppHost/Examples.Aspire.AppHost.csproj
```

- Open the Aspire dashboard to see all services running
- Open `https://one.eshop.local:8443/` or `https://two.eshop.local:8443/`

## API Reference

### Configuration API

The runtime configuration API is exposed at the configured route prefix (default: `/`).
An optional route prefix can be provided during configuration.

#### Routes

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/route` | List all configured routes |
| `POST` | `/route` | Add or update routes |

#### Clusters

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/cluster` | List all configured clusters |
| `POST` | `/cluster` | Add or update clusters |

### Noteworthy Extension Methods

#### `UseSelfSignedCertificate()`
Enables automatic self-signed certificate generation for HTTPS endpoints.

#### `ConfigureReverseProxy()`
Registers reverse proxy services in the DI container.

#### `UseReverseProxy()`
Adds the reverse proxy middleware.

#### `UseReverseProxyApi()`
Maps the runtime configuration API endpoints with an optional route prefix.

#### `UseBlackholeCatchAll()`
Adds a blackhole route that causes unmatched routes to be ignored.

#### `WithReverseProxyReference()`
Configures Aspire service discovery for a resource endpoint. Its required
`forwardPublicOrigin` argument controls whether the proxy supplies the secure public
origin header contract.

## Troubleshooting

### Certificate trust issues

If you encounter certificate trust warnings:
- **Windows**: The CA must be manually installed in the `Trusted Root Certification Authorities` store
- **macOS**: You need to manually trust the CA in Keychain Access
- **Linux**: Add the CA certificate to your system's trust store (location varies by distribution)

### Port conflicts

If you see port binding errors:
- Ports below 1024 (like 443) require administrator/root privileges when running `dotnet run`

### Aspire service discovery not working

Ensure that the referenced endpoint belongs to the target resource and that its HTTP
endpoint is available. `WithReverseProxyReference()` derives the service-discovery name
from that resource.
