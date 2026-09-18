using Hj.ReverseProxy.Aspire;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var reverseProxyHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".reverseproxy");

Directory.CreateDirectory(reverseProxyHome);

// Add example websites. Both resources use the same project but return a distinct target name.
var websiteOne = builder.AddProject<Examples_Aspire_Website>("website-one", options =>
  {
    // Do not use endpoint configuration found in the Website project. We let Aspire set up everything.
    options.ExcludeLaunchProfile = true;
    options.ExcludeKestrelEndpoints = true;
  })
  // Add a HTTP endpoint. The reverse proxy will set up a secure HTTPS endpoint for you to connect to this resource.
  .WithHttpEndpoint()
  .WithEnvironment("EXAMPLE_TARGET_NAME", "one");

var websiteTwo = builder.AddProject<Examples_Aspire_Website>("website-two", options =>
  {
    options.ExcludeLaunchProfile = true;
    options.ExcludeKestrelEndpoints = true;
  })
  .WithHttpEndpoint()
  .WithEnvironment("EXAMPLE_TARGET_NAME", "two");

// Add reverse proxy website. The following shows 3 scenarios for configuring HTTPS endpoints.
var reverseProxy = builder
  .AddProject<Examples_Aspire_ReverseProxy>("Reverse-Proxy")
  .WithExternalHttpEndpoints()
  .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
  .WithEnvironment("REVERSEPROXY_HOME", reverseProxyHome); // Made for developers.

// Configure the reverse proxy to use HTTPS on port 443 to allow nice urls without port numbers. This requires admin/root
// privileges when starting the apphost and may not work if you forward ports for remote development.
// reverseProxy.WithHttpsEndpoint(443);

// Using a different port above 1024 for HTTPS avoids requiring the admin/root privileges mentioned above. But now we
// get port numbers in the urls which we probably won't have when deploying to production. This is not ideal but makes
// remote development using a port forward easier.
reverseProxy.WithHttpsEndpoint(port: 8443);

// For remote development without port forwarding or dev tunnels, we can disable the Aspire proxying and configure Kestrel
// to bind to all interfaces. Unfortunately this means that we can no longer use Aspire scaling but for development this
// is often desired.
// reverseProxy.WithHttpsEndpoint(port: 8443, isProxied: false)
//   .WithEnvironment("ASPNETCORE_URLS", "https://0.0.0.0:8443");

// Add each website with a nice host name. Since we apply HTTPS using the reverse proxy by configuring the endpoint above,
// we can use the HTTP endpoint of each proxied website.
reverseProxy
  .WithReverseProxyReference(websiteOne.GetEndpoint("http"), "one.eshop.local", forwardPublicOrigin: true)
  .WithReverseProxyReference(websiteTwo.GetEndpoint("http"), "two.eshop.local", forwardPublicOrigin: true);

// Wait for both websites to be healthy before starting the reverse proxy.
reverseProxy
  .WaitFor(websiteOne)
  .WaitFor(websiteTwo);

await builder.Build().RunAsync();
