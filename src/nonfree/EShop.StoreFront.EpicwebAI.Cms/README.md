# Storefront Epicweb AI CMS integration

Epicweb AI Assistant is a paid product. The
`Epicweb.Optimizely.AIAssistant` and `Epicweb.Optimizely.AIAssistant.Tools` package
dependencies make this project a paid-product consumer.

`EShop.StoreFront.EpicwebAI.Cms` owns the Optimizely CMS integration for the Epicweb AI
Assistant (<https://github.com/Epicweb-Optimizely/Epicweb.Optimizely.AIAssistant/>). It
packages the Epicweb dependencies and registers the fixed CMS tool set that the
Storefront AI bridge can request.

The project registers these tools:

- Core CMS tools (`BuiltinTools`).
- Content chat and read tools (`BuiltinChatTools`).
- Content publish tools (`BuiltinPublishChatTools`).
- Content update tools (`BuiltinUpdateChatTools`).

It also maps the Epicweb SignalR hub that CMS chat requires. It does not configure an AI
provider, store an Anthropic credential, or add a browser endpoint outside the Storefront
CMS application.

## Use from a CMS host

Add a project reference to this project. Its Epicweb package dependencies flow through
the project reference, so the CMS host does not need direct Epicweb package references.

In `Startup.ConfigureServices`, add the CMS services:

```csharp
services.AddEpicwebAiAssistantCms();
```

In the application's routed endpoint configuration, map the required hub:

```csharp
app.UseEndpoints(endpoints =>
{
    endpoints.MapEpicwebAiAssistantHub();
});
```

Call the service-registration method during startup before the endpoint mapping. The hub
method requires routing to be enabled and must be called in the application's endpoint
configuration.

## Tool authorization

The tools run inside the CMS host. Optimizely CMS remains responsible for authorizing
read, update, and publish actions, including its approval rules. This project does not
grant CMS permissions.
