# Cirreum Runtime InvocationProvider

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Runtime.InvocationProvider.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.InvocationProvider/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Runtime.InvocationProvider.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.InvocationProvider/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Runtime.InvocationProvider?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Runtime.InvocationProvider/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Runtime.InvocationProvider?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Runtime.InvocationProvider/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Runtime-layer registration helper for the Cirreum Invocation provider family.**

## Overview

`Cirreum.Runtime.InvocationProvider` is the Runtime-layer library that bootstraps any `InvocationProviderRegistrar<TSettings, TInstanceSettings>` from configuration. Single responsibility: given a registrar type, bind the corresponding config section and call both phases of the registrar's lifecycle correctly. It also exposes the app-facing `IInvocationBuilder` seam and the top-level `AddInvocation()` entry point. The endpoints-phase mapping (`MapInvocation()` / per-source `Map*Invocation()`) lives in the Runtime Extensions layer (L5).

Apps do **not** reference this package directly — they install a Runtime Extensions package such as `Cirreum.Runtime.Invocation.SignalR` or `Cirreum.Runtime.Invocation.WebSockets`, which use this helper internally. Apps that only use HTTP invocations never reference it at all — HTTP is composed automatically by `Cirreum.Services.Server`.

`Cirreum.Runtime.Server` does **not** reference this package either (no intra-layer L4 references). Apps that use long-lived invocation sources get this package transitively through whichever L5 source package they install.

## Two-phase registration recap

Invocation provider registrars run in two phases:

1. **Services phase** (before `builder.Build()`) — `Register(settings, services, configuration)` wires up DI.
2. **Endpoints phase** (after `builder.Build()`) — `Map(settings, endpoints)` maps HTTP routes (`MapHub<THub>`, raw WebSocket endpoints, etc.).

`IEndpointRouteBuilder` isn't available at builder time, so this helper runs phase 1 immediately and **stashes a closure** for phase 2 as a DI singleton (an `InvocationProviderMapping`). The Runtime Extensions layer pulls the stashed closures at `MapInvocation()` / `Map*Invocation()` call time and invokes them against the live `IEndpointRouteBuilder`.

## API

### `RegisterInvocationProvider<TRegistrar, TSettings, TInstanceSettings>()`

```csharp
using Microsoft.Extensions.Hosting;

builder.RegisterInvocationProvider<
    SignalRInvocationRegistrar,
    SignalRInvocationProviderSettings,
    SignalRInvocationProviderInstanceSettings>();
```

Generally called from inside per-source Runtime Extension packages (`AddSignalR<THub>()`, `AddWebSocket<THandler>()`), not from app code.

**What it does:**

1. Dedup check via marker-type registration — repeated calls for the same `TRegistrar` are no-ops.
2. Binds `Cirreum:Invocation:Providers:{ProviderName}` from `IConfiguration` to `TSettings`.
3. Skips with a debug log if the section is missing (or throws if `required: true` was passed).
4. Calls `registrar.Register(providerSettings, services, configuration)` — services phase.
5. Registers an `InvocationProviderMapping` in DI capturing a closure over `registrar.Map(providerSettings, endpoints)` — deferred endpoints phase.

### `IInvocationBuilder` / `InvocationBuilder`

The fluent configuration builder passed into the `AddInvocation(configure)` callback. Per-source Runtime Extensions (`AddSignalR<THub>()`, `AddWebSocket<THandler>()`) are extension methods *on* `IInvocationBuilder` — apps compose them with the typed-handle pattern:

```csharp
builder.AddInvocation(b => b
    .AddSignalR<ChatHub>("chat")
    .AddSignalR<NotificationHub>("notifications")
    .AddWebSocket<VoiceFrameHandler>("voice"));
```

Each per-source extension calls `builder.HostBuilder.RegisterInvocationProvider<...>()` internally so the registrar's two-phase lifecycle runs correctly. `HostBuilder` is exposed on the builder for advanced scenarios that need the underlying `IHostApplicationBuilder`.

Defined here (not duplicated per Runtime Extensions package) so the callback-based API composes identically regardless of which source packages the app installs.

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

### Typed `Items`-slot helpers (`Cirreum.Security.InvocationContextAuthenticationExtensions`)

Typed extension methods on `IInvocationContext` for the well-known `AuthenticationContextKeys` slots:

- `GetAuthenticatedScheme()` / `SetAuthenticatedScheme(string)`
- `GetApplicationUserCache()` / `SetApplicationUserCache(IApplicationUser)`

Provides type-safety over the raw `Items[...]` dictionary access used by upstream writers (the role-claims transformer) and downstream readers (`UserStateAccessor`, the conductor pipeline).

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

- **Cirreum.InvocationProvider** — L2 abstractions (`IInvocationContext`, `IInvocationConnection`, `InvocationProviderRegistrar`)
- **Cirreum.Logging.Deferred** — deferred logging for startup diagnostics
- **Microsoft.AspNetCore.App** — `IEndpointRouteBuilder`

## Versioning

Follows [Semantic Versioning](https://semver.org/). Foundational library — major bumps are rare and coordinated with `Cirreum.InvocationProvider` releases.

## License

MIT — see [LICENSE](LICENSE).

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
