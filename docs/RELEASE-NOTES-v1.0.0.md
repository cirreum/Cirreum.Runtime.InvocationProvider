# Cirreum.Runtime.InvocationProvider 1.0.0 — L4 runtime helper for the Invocation provider family

Initial release. This package is the L4 Runtime counterpart to the L2 `Cirreum.InvocationProvider` abstractions: it bootstraps any `InvocationProviderRegistrar<TSettings, TInstanceSettings>` from configuration, runs both phases of the registrar's lifecycle correctly, and exposes the app-facing `IInvocationBuilder` seam and `AddInvocation()` entry point. Mirrors the `Cirreum.Runtime.IdentityProvider` pattern.

Anchored by [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md). Release #4 in the [Invocation family rollout](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/InvocationContext/03-MIGRATION.md).

---

## Why this release exists

`Cirreum.InvocationProvider 1.0.1` shipped the L2 abstractions: `IInvocationContext`, `IInvocationConnection`, `IConnectionLifecycle`, `IConnectionSender`, plus the `InvocationProviderRegistrar` base type that L3 per-source packages derive from. What was missing was the L4 piece every multi-instance Cirreum provider track needs — a config-driven helper that:

1. Binds the right configuration section to the registrar's typed settings.
2. Runs the registrar's services-phase `Register(...)` immediately (during builder time).
3. Stashes the registrar's endpoints-phase `Map(...)` as a DI-resident closure for later invocation.
4. Exposes a fluent app-facing seam so apps compose multiple invocation sources naturally.

`Cirreum.Runtime.IdentityProvider` is the prior art for this pattern. This package is the same SRP-split applied to Invocation, with one structural difference: the endpoints-phase closure invocation lives at L5 (next to ASP.NET's matching primitives like `MapHub<THub>()`), not here.

---

## What's new

### App-facing builder seam (`Cirreum.Invocation` namespace)

```csharp
public interface IInvocationBuilder {
    IHostApplicationBuilder HostBuilder { get; }
}
```

Fluent configuration builder passed into the `AddInvocation(configure)` callback. Per-source Runtime Extensions (`AddSignalR<THub>()`, `AddWebSocket<THandler>()`) are extension methods *on* `IInvocationBuilder` — apps compose them with a typed-handle pattern:

```csharp
builder.AddInvocation(b => b
    .AddSignalR<ChatHub>("chat")
    .AddSignalR<NotificationHub>("notifications")
    .AddWebSocket<VoiceFrameHandler>("voice"));
```

Each per-source extension calls `builder.HostBuilder.RegisterInvocationProvider<...>()` internally so the registrar's two-phase lifecycle runs correctly. `HostBuilder` is exposed for advanced scenarios that need the underlying `IHostApplicationBuilder`.

Defined here (not duplicated per Runtime Extensions package) so the callback-based API composes identically regardless of which source packages the app installs.

### Host application extensions (`Microsoft.Extensions.Hosting` namespace)

- **`AddInvocation(Action<IInvocationBuilder>)`** — top-level entry point invoked once per host startup. Surfaced in the framework namespace so consumers get it for free.
- **`RegisterInvocationProvider<TRegistrar, TSettings, TInstanceSettings>()`** — config-driven provider registration helper. Generally called from inside per-source Runtime Extension packages, not from app code directly. Mirrors the AuthZ/Identity track helpers.

### Two-phase registration lifecycle

`RegisterInvocationProvider<TRegistrar, TSettings, TInstanceSettings>()`:

1. Dedup check via marker-type registration — repeated calls for the same `TRegistrar` are no-ops.
2. Binds `Cirreum:Invocation:Providers:{ProviderName}` from `IConfiguration` to `TSettings`.
3. Skips with a debug log if the section is missing (or throws if `required: true` was passed).
4. Calls `registrar.Register(providerSettings, services, configuration)` — services phase, immediate.
5. Registers an `InvocationProviderMapping` in DI capturing a closure over `registrar.Map(providerSettings, endpoints)` — endpoints phase, deferred.

`IEndpointRouteBuilder` isn't available at builder time, hence the DI-stash pattern. The closure runs later when an L5 Runtime Extensions package resolves `IEnumerable<InvocationProviderMapping>` and invokes the matching mappings against the live `IEndpointRouteBuilder`.

### Deferred-mapping record

```csharp
public sealed record InvocationProviderMapping(
    string ProviderName,
    Action<IEndpointRouteBuilder> Map);
```

Public record — Runtime Extensions packages resolve `IEnumerable<InvocationProviderMapping>` from DI at their own `Map*Invocation()` call time:

- The **umbrella** `Cirreum.Runtime.Invocation` exposes `MapInvocation()` which invokes every registered mapping.
- A **per-source** package such as `Cirreum.Runtime.Invocation.SignalR` exposes `MapSignalRInvocation()` which filters by `ProviderName == "SignalR"` and invokes just those — the natural pair to ASP.NET's built-in `MapHub<THub>()` for apps that compose mapping per-source.

This package deliberately does not provide a one-size-fits-all endpoints-phase entry point itself. The choice between umbrella-level and per-source mapping belongs to the L5 layer, where each per-source package can compose with the matching ASP.NET primitives under a coherent name.

### Typed `Items`-slot helpers (`Cirreum.Security` namespace)

`InvocationContextAuthenticationExtensions` provides typed extension methods on `IInvocationContext` for the well-known `AuthenticationContextKeys` slots:

- `GetAuthenticatedScheme()` / `SetAuthenticatedScheme(string)`
- `GetApplicationUserCache()` / `SetApplicationUserCache(IApplicationUser)`

Type-safety over the raw `Items[...]` dictionary access used by upstream writers (the role-claims transformer) and downstream readers (`UserStateAccessor`, the conductor pipeline). L4+ consumers depend on these in preference to direct dictionary access against the key constants; L3 framework code retains raw access for the few centralized reads/writes it does.

---

## Architecture position

```
L2 Core
  Cirreum.InvocationProvider              ← abstractions: IInvocationContext, registrar bases, ...

L3 Infrastructure
  Cirreum.Invocation.SignalR              ← per-source concrete registrar (Phase 5)
  Cirreum.Invocation.WebSockets           ← per-source concrete registrar (Phase 5)

L4 Runtime
  Cirreum.Runtime.InvocationProvider      ← THIS PACKAGE — IInvocationBuilder, AddInvocation, RegisterInvocationProvider

L5 Runtime Extensions
  Cirreum.Runtime.Invocation.SignalR      ← refs L3 + L4; surfaces AddSignalR<THub>(key) + MapSignalRInvocation()
  Cirreum.Runtime.Invocation.WebSockets   ← same shape
  Cirreum.Runtime.Invocation              ← umbrella; MapInvocation() invokes all mappings
```

Apps with only HTTP invocations **do not** reference this package — HTTP is composed automatically by `Cirreum.Services.Server`. Apps that use long-lived invocation sources get this package transitively through whichever L5 source package they install.

`Cirreum.Runtime.Server` does **not** reference this package either (no intra-layer L4 references).

---

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

Where `{ProviderName}` is set by the concrete L3 registrar (e.g., `"SignalR"`, `"WebSocket"`).

---

## Dependencies

- **`Cirreum.Core`** `5.1.0` — cross-host foundation
- **`Cirreum.InvocationProvider`** `1.0.1` — L2 abstractions this package bootstraps
- **`Cirreum.Logging.Deferred`** `1.0.113` — deferred logging for startup diagnostics
- **`Microsoft.AspNetCore.App`** (framework reference) — `IEndpointRouteBuilder`

---

## What this enables

This release unblocks the L5 invocation-source packages (releases #11–#16 in the migration matrix):

- `Cirreum.Invocation.SignalR` (L3) — concrete `SignalRInvocationRegistrar`
- `Cirreum.Runtime.Invocation.SignalR` (L5) — `AddSignalR<THub>()` + `MapSignalRInvocation()` extensions, `IConnectionSender` impl
- `Cirreum.Runtime.Invocation.SignalR.Wasm` (L5) — client-side adapter
- `Cirreum.Invocation.WebSockets` / `Cirreum.Runtime.Invocation.WebSockets` / `.Wasm` — same shape
- `Cirreum.Runtime.Invocation` (L5) — umbrella with `MapInvocation()` for "all sources"
- `Cirreum.Runtime.Invocation.Wasm` (L5) — client-side umbrella

Each of these will reference `Cirreum.Runtime.InvocationProvider 1.0.0+` for the registration helper and the `IInvocationBuilder` seam.

---

## Compatibility

- **Initial release.** No prior version, no migration story.
- Stable public surface — the `IInvocationBuilder`, `RegisterInvocationProvider<>`, and `InvocationProviderMapping` shapes are intended to evolve only through additive minor bumps.

---

## See also

- `CHANGELOG.md` — condensed change list for `1.0.0`.
- [`Cirreum.InvocationProvider 1.0.1`](https://www.nuget.org/packages/Cirreum.InvocationProvider) — L2 abstractions this package bootstraps.
- [`Cirreum.Runtime.IdentityProvider`](https://www.nuget.org/packages/Cirreum.Runtime.IdentityProvider) — the prior-art SRP-split that this package mirrors.
- [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md) — the foundational design decision.
- [Provider-pattern integration](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/InvocationContext/02-PROVIDER-PATTERN.md) — how registrars compose with the Provider track.
- [Migration & sequencing](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/InvocationContext/03-MIGRATION.md) — full rollout plan.
