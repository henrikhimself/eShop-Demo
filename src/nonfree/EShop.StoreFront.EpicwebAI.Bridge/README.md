# Storefront Epicweb AI bridge

Epicweb AI Assistant is a paid product. This bridge is a paid-product consumer because
it exists solely to provide the Anthropic service for Epicweb AI Assistant CMS chat.

`EShop.StoreFront.EpicwebAI.Bridge` is a private ASP.NET Core service that translates
Epicweb AI Assistant requests from the Storefront CMS to Anthropic Messages API
requests. It enables Claude CMS chat with the tool calls registered by
`EShop.StoreFront.EpicwebAI.Cms`.

The bridge has no browser ingress. The Storefront is its only caller. The AppHost
injects the bridge endpoint into the Storefront's `Epicweb:AIAssistant:ApiUrl` setting.

## Responsibilities

- Accept Epicweb suggestion and chat requests.
- Send supported requests to Anthropic Claude.
- Translate Anthropic text and tool-use responses to the Epicweb response format.
- Keep short-lived pending tool-call state in the shared Redis cache until the CMS sends
  its tool result.
- Reject image-generation and vision requests. These features are not supported.

The bridge does not authorize CMS actions. The CMS host executes tools and applies its
own authorization and approval rules.

## Run locally

Run the solution through the AppHost:

```bash
./scripts/eshop.sh run --agent
```

Before starting it, set the AppHost user-secret parameter
`epicweb-aiassistant-anthropic-apikey` to a valid Anthropic API key. The AppHost injects
that value only as `Anthropic:ApiKey` in the bridge process. Do not add this key to
Storefront configuration, browser configuration, or source-controlled settings.

The AppHost starts the bridge as the private `storefront-epicweb-ai` resource and gives
it the shared cache connection. The Storefront receives the bridge URL from the AppHost;
do not configure a public bridge URL.

## Configuration

The bridge commits only the Anthropic API endpoint:

```json
{
  "Anthropic": {
    "Endpoint": "https://api.anthropic.com/"
  }
}
```

`Anthropic:ApiKey` is required at startup. The bridge validates this value and fails to
start if it is missing. Anthropic generation requests have a two-minute total and
per-attempt timeout.

## Deployment

The bridge is designed to run as a private Azure Container App. Deployment must not
provision this service until the Anthropic secret provisioning and rotation mechanism is
decided and implemented.
