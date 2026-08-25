> [!WARNING]
> **This package is deprecated and this repository is archived.**
>
> The Invocation provider family is no longer maintained. The inbound invocation seam ships today in
> `Cirreum.Services.Server` — hubs, WebSocket handlers, and the HTTP-to-invocation-context bridge —
> and the caller side of a long-lived connection ships as `Cirreum.RemoteConnections.SignalR` and
> `Cirreum.RemoteConnections.WebSockets`.
>
> Everything below this line is retained as a historical record and does not describe a supported
> package.

---

# Cirreum Runtime InvocationProvider

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Runtime.InvocationProvider.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.InvocationProvider/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Runtime.InvocationProvider.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.InvocationProvider/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Runtime.InvocationProvider?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Runtime.InvocationProvider/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Runtime.InvocationProvider?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Runtime.InvocationProvider/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Runtime-layer registration helper for the Cirreum Invocation provider family.**

## Overview

`Cirreum.Runtime.InvocationProvider` is the Runtime-layer library that bootstraps any `InvocationProviderRegistrar<TSettings, TInstanceSettings>` from configuration. Single responsibility: given a registrar type, bind the corresponding config section and call both phases of the registrar's lifecycle correctly. It also exposes the `IInvocationBuilder` scope object that L5 invocation-source extensions attach their per-instance methods to (`AddSignalR<THub>`, `AddWebSocket<THandler>`, …).

App-facing entry points (`AddSignalRInvocation`, `AddWebSocketInvocation`, umbrella `AddInvocation`) and the endpoints-phase mapping (`MapSignalRInvocation`, `MapInvocation`) live in the L5 Runtime Extensions packages — exact mirror of the Identity track's `AddOidcIdentity` / `AddIdentity` shape.

Apps do **not** reference this package directly — they install a Runtime Extensions package such as `Cirreum.Runtime.Invocation.SignalR` or the umbrella `Cirreum.Runtime.Invocation`, which pull this helper in transitively. Apps that only use HTTP invocations never reference it at all — HTTP is composed automatically by `Cirreum.Services.Server`.

`Cirreum.Runtime.Server` does **not** reference this package either (no intra-layer L4 references). Apps that use long-lived invocation sources get this package transitively through whichever L5 source package they install.

## Two-phase registration recap

Invocation provider registrars run in two phases:

1. **Services phase** (before `builder.Build()`) — `Register(settings, services, configuration)` wires up DI.
2. **Endpoints phase** (after `builder.Build()`) — `Map(settings, endpoints)` maps HTTP routes (`MapHub<THub>`, raw WebSocket endpoints, etc.).

`IEndpointRouteBuilder` isn't available at builder time, so this helper runs phase 1 immediately and **stashes a closure** for phase 2 as a DI singleton (an `InvocationProviderMapping`). The L5 Runtime Extensions layer pulls the stashed closures at `MapInvocation()` / `Map*Invocation()` call time and invokes them against the live `IEndpointRouteBuilder`.

## API

### `RegisterInvocationProvider<TRegistrar, TSettings, TInstanceSettings>()`

```csharp
using Microsoft.Extensions.Hosting;

builder.RegisterInvocationProvider<
    SignalRInvocationRegistrar,
    SignalRInvocationSettings,
    SignalRInvocationInstanceSettings>();
```

Called from inside L5 invocation-source entry points (`AddSignalRInvocation`, `AddWebSocketInvocation`, …), not from app code.

**What it does:**

1. Dedup check via marker-type registration — repeated calls for the same `TRegistrar` are no-ops.
2. Binds `Cirreum:Invocation:Providers:{ProviderName}` from `IConfiguration` to `TSettings`.
3. Skips with a debug log if the section is missing (or throws if `required: true` was passed).
4. Calls `registrar.Register(providerSettings, services, configuration)` — services phase.
5. Registers an `InvocationProviderMapping` in DI capturing a closure over `registrar.Map(providerSettings, endpoints)` — deferred endpoints phase.

### `IInvocationBuilder` / `InvocationBuilder`

A minimal scope object holding a single `HostBuilder` property. Created by L5 invocation-source entry points and passed to the caller's configuration callback so per-instance extension methods (`AddSignalR<THub>`, `AddWebSocket<THandler>`, …) — defined on `IInvocationBuilder` by their respective L5 packages — can be chained naturally:

```csharp
// Per-source entry point (lives in Cirreum.Runtime.Invocation.SignalR):
builder.AddSignalRInvocation(b => b
    .AddSignalR<ChatHub>("chat")
    .AddSignalR<NotificationHub>("notifications"));

// Umbrella entry point (lives in Cirreum.Runtime.Invocation):
builder.AddInvocation(b => b
    .AddSignalR<ChatHub>("chat")
    .AddWebSocket<VoiceFrameHandler>("voice"));
```

`IInvocationBuilder` is intentionally minimal — mirrors `IIdentityBuilder`'s shape. Per-instance source-specific knowledge lives in the L5 packages; L4 just provides the scope object and the registration helper.

### `InvocationProviderMapping` (stashed in DI)

```csharp
public sealed record InvocationProviderMapping(
    string ProviderName,
    Action<IEndpointRouteBuilder> Map);
```

Public record — Runtime Extensions packages resolve `IEnumerable<InvocationProviderMapping>` from DI at their own `Map*Invocation()` call time:

- The **umbrella** `Cirreum.Runtime.Invocation` exposes `MapInvocation()` which invokes every registered mapping (regardless of source).
- A **per-source** package such as `Cirreum.Runtime.Invocation.SignalR` exposes `MapSignalRInvocation()` which filters by `ProviderName == "SignalR"` and invokes just those — the natural pair to ASP.NET's built-in `MapHub<THub>()` for apps that compose mapping per-source.

This package deliberately does not provide a one-size-fits-all endpoints-phase entry point itself; the choice between umbrella-level and per-source mapping belongs to the L5 layer, where each per-source package can compose with the matching ASP.NET primitives (`MapHub<THub>()`, etc.) under a coherent name.

## Configuration

The package binds settings from `Cirreum:Invocation:Providers:{ProviderName}`:

```json
{
  "Cirreum": {
    "Invocation": {
      "Providers": {
        "SignalR": {
          "Instances": {
            "chat":          { "Enabled": true, "Path": "/chat",          "Scheme": "oidc_primary" },
            "notifications": { "Enabled": true, "Path": "/notifications", "Scheme": "oidc_primary" }
          }
        },
        "WebSocket": {
          "Instances": {
            "voice": { "Enabled": true, "Path": "/voice", "Scheme": "oidc_primary" }
          }
        }
      }
    }
  }
}
```

## Dependencies

- **Cirreum.Core** `5.1.0+` — cross-host foundation
- **Cirreum.InvocationProvider** `1.1.0+` — L2 abstractions (`IInvocationContext`, `IInvocationConnection`, `IConnectionLifecycle`, `DisconnectInfo`, `InvocationProviderRegistrar`)
- **Cirreum.Logging.Deferred** — deferred logging for startup diagnostics
- **Microsoft.AspNetCore.App** (framework reference) — `IEndpointRouteBuilder`

## Versioning

Follows [Semantic Versioning](https://semver.org/). Foundational library — major bumps are rare and coordinated with `Cirreum.InvocationProvider` releases.

## License

MIT — see [LICENSE](LICENSE).

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
