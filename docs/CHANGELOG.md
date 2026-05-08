# Changelog

All notable changes to **Cirreum.Runtime.InvocationProvider** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

Initial release of the Cirreum Invocation Provider runtime library — the L4 piece that bootstraps L3 per-source `InvocationProviderRegistrar` impls and exposes the app-facing `IInvocationBuilder` seam, `AddInvocation()` entry point, and `MapInvocationEndpoints()` endpoint-mapping helper. Mirrors the `Cirreum.Runtime.IdentityProvider` pattern. Anchored by [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md).

**App-facing builder seam:**

- `IInvocationBuilder` (`Cirreum.Invocation`) — fluent configuration builder passed to the `AddInvocation(configure)` callback. L5 invocation-source packages surface `Add{Source}<T>(key)` extension methods on this interface.
- `InvocationBuilder` — default implementation with per-call instance dedup tracking.

**Host application extensions (in `Microsoft.Extensions.Hosting` namespace, surfaced for free):**

- `AddInvocation(Action<IInvocationBuilder>)` — top-level entry point invoked once per host startup.
- `RegisterInvocationProvider<TRegistrar, TSettings, TInstanceSettings>()` — config-driven provider registration helper called by L5 packages from inside their `Add{Source}<T>(key)` extensions; mirrors the AuthZ/Identity track helpers.

**Endpoint-mapping extension (in `Microsoft.AspNetCore.Routing` namespace):**

- `MapInvocationEndpoints()` on `IEndpointRouteBuilder` — invokes every deferred `InvocationProviderMapping` stashed in DI during the services phase. Safe to call when no providers are registered (no-op).

**Deferred-mapping record:**

- `InvocationProviderMapping(string ProviderName, Action<IEndpointRouteBuilder> Map)` — per-provider endpoint-mapping closure stashed as DI singleton during services phase, executed by `MapInvocationEndpoints()` after `Build()`.

**Helpers:**

- `Helpers/InvocationItemsCopyHelper.CopyAuthenticationSlots(HttpContext, IDictionary<object, object?>)` — centralized upgrade-time copy of `AuthenticationContextKeys` slots from `HttpContext.Items` to `IInvocationConnection.Items` (in `Cirreum.Invocation.Connections`). Used by L5 long-lived adapters to satisfy ADR-0002 transport-adapter invariant #2.

**Standardized `Items`-slot access (in `Cirreum.Security` namespace):**

- `InvocationContextAuthenticationExtensions` — typed `Get*`/`Set*` extension methods wrapping `AuthenticationContextKeys` slots on `IInvocationContext`. Consumers at L4+ depend on these in preference to direct dictionary access against the key constants. (L3 framework code uses raw access for the few centralized reads/writes it does.)

### Architecture position

This package is the **L4 Runtime** counterpart to the L2 `Cirreum.InvocationProvider` abstractions. Apps with only HTTP invocations don't reference it (the HTTP path is handled by `Cirreum.Services.Server` directly). Apps that use long-lived invocation sources (SignalR, WebSocket) get this package transitively through their L5 source package (e.g., `Cirreum.Runtime.Invocation.SignalR`).

No intra-layer references — `Cirreum.Runtime.Server` does not reference this package; the L5 source packages reference both their L3 per-source base AND this L4 runtime helper.
