using Hj.Examples.Aspire.ServiceDefaults;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.Use(async (context, next) =>
{
  context.Items["OriginalHost"] = context.Request.Host.Value;
  context.Items["XForwardedFor"] = context.Request.Headers["X-Forwarded-For"].ToString();
  context.Items["XForwardedHost"] = context.Request.Headers["X-Forwarded-Host"].ToString();
  context.Items["XForwardedProto"] = context.Request.Headers["X-Forwarded-Proto"].ToString();

  await next();
});

// Enable handling forwarded headers.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
  ForwardedHeaders = ForwardedHeaders.All,
});

app.MapDefaultEndpoints();

app.MapGet("/target", (IConfiguration configuration) => configuration["EXAMPLE_TARGET_NAME"] ?? "unknown");

app.MapGet("/headers", (HttpContext context) => Results.Json(new
{
  Host = context.Request.Host.Value,
  OriginalHost = context.Items["OriginalHost"],
  XForwardedFor = context.Items["XForwardedFor"],
  XForwardedHost = context.Items["XForwardedHost"],
  XForwardedProto = context.Items["XForwardedProto"],
}));

app.UseRouting();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}");

await app.RunAsync();
