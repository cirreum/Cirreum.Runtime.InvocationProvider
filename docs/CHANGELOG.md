# Changelog

All notable changes to **Cirreum.Runtime.InvocationProvider** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Updated

- Updated NuGet packages.

## [1.2.0] - 2026-05-10

### Changed

- **Removed `InvocationContextAuthenticationExtensions`** (`Cirreum.Security` namespace) — the typed `GetAuthenticatedScheme` / `SetAuthenticatedScheme` / `GetApplicationUserCache` / `SetApplicationUserCache` extensions on `IInvocationContext`. These were originally added on the speculative premise of "consumers might want type-safe access to the auth `Items`-slot keys," but on review the framework's actual posture is that **app code should not be reading or writing these slots at all** — they're framework-internal cache state. App code reads through the canonical APIs (`IUserStateAccessor.GetUser()` → `IUserState`); framework writers (`AudienceProviderRoleClaimsTransformer`, the dynamic-scheme forward selector, `UserStateAccessor.ResolveApplicationUserAsync`) use raw dictionary access against `AuthenticationContextKeys.*` consts as their established pattern. Exposing typed setters created an attractive nuisance: app developers who shouldn't be touching these slots would discover the typed setters and assume that's a sanctioned path. Removing the surface eliminates the nuisance and clarifies the contract — there's no public way for app code to fiddle with the auth cache.

  Captured as `### Changed` (not `### Removed`) under the same window-of-no-consumers, framework-owned-implementer-set precedent as the L2 1.1.0 / 1.2.0 / 1.3.0 cascades — these extensions are a v1.x pre-adoption surface; no app or framework code calls them today (verified via codebase grep). The deletion is a Minor under the established precedent rather than a Major.

## [1.1.2] - 2026-05-09

### Updated

- Updated NuGet packages.

## [1.1.1] - 2026-05-09

### Updated

- Updated NuGet packages.

## [1.1.0] - 2026-05-08

### Changed

- **`IInvocationBuilder` slimmed down to just `HostBuilder`.** The redundant `Services`, `Configuration`, `RegisteredInstances`, and `TrackInstance` members were dropped — all reachable via `HostBuilder.Services` / `HostBuilder.Configuration` or duplicated work the L2 registrar's static `ProcessedInstances` tracker already does authoritatively. Now mirrors `IIdentityBuilder`'s minimalism — the builder's only job is to scope per-source extension methods to a clear site in `Program.cs`.
- **App-facing entry point relocated from L4 to L5.** `AddInvocation(this IHostApplicationBuilder, Action<IInvocationBuilder>)` is no longer in this package — its responsibility moves to the L5 Runtime Extensions layer where it structurally belongs. Per-source packages (`Cirreum.Runtime.Invocation.SignalR`, `Cirreum.Runtime.Invocation.WebSockets`, future `*.Grpc`) each surface their own `Add{Source}Invocation()` entry point on `IHostApplicationBuilder`, and the umbrella `Cirreum.Runtime.Invocation` package surfaces a unified `AddInvocation()` that registers every shipped source. This is the exact mirror of the Identity track's `AddOidcIdentity` / `AddEntraExternalIdIdentity` / umbrella `AddIdentity` shape.

### Updated

- **`Cirreum.InvocationProvider`** floor `1.0.1` → `1.1.0`. Picks up the `DisconnectInfo` parameter on `IConnectionLifecycle.OnDisconnectedAsync` and aligns the L4 floor with the L5 SignalR adapter (which floors at L2 1.1.0).

### Migration from 1.0.0

For framework-internal consumers and L5 invocation-source extension authors:

- `IInvocationBuilder.HostBuilder` is unchanged — the only retained member.
- `IInvocationBuilder.Services` → `builder.HostBuilder.Services`
- `IInvocationBuilder.Configuration` → `builder.HostBuilder.Configuration`
- `IInvocationBuilder.RegisteredInstances` / `TrackInstance` → no replacement needed; the L2 `InvocationProviderRegistrar` base already prevents duplicate instance keys via its static `ProcessedInstances` tracker.
- `builder.AddInvocation(b => ...)` no longer exists in this package. Apps install an L5 Runtime Extensions package (e.g. `Cirreum.Runtime.Invocation.SignalR`) and call its per-source entry point (e.g. `builder.AddSignalRInvocation(b => b.AddSignalR<THub>("key"))`), or the umbrella's `AddInvocation()` for the kitchen-sink case.

No published consumers of v1.0.0 are known to exist — the L5 Runtime Extensions packages that would consume this haven't shipped yet, and `Cirreum.Runtime.Server` does not reference this package (no intra-layer L4 reference). Same window-of-no-consumers reasoning that motivated the L2 corrections in 1.0.1 / 1.1.0.

## [1.0.0] - 2026-05-07

### Added

Initial release of the Cirreum Invocation Provider runtime library — the L4 piece that bootstraps L3 per-source `InvocationProviderRegistrar` impls and exposes the app-facing `IInvocationBuilder` seam plus `AddInvocation()` entry point. Mirrors the `Cirreum.Runtime.IdentityProvider` pattern. Anchored by [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md).

**App-facing builder seam:**

- `IInvocationBuilder` (`Cirreum.Invocation`) — fluent configuration builder passed to the `AddInvocation(configure)` callback. L5 invocation-source packages surface `Add{Source}<T>(key)` extension methods on this interface.
- `InvocationBuilder` — default implementation with per-call instance dedup tracking.

**Host application extensions (in `Microsoft.Extensions.Hosting` namespace, surfaced for free):**

- `AddInvocation(Action<IInvocationBuilder>)` — top-level entry point invoked once per host startup.
- `RegisterInvocationProvider<TRegistrar, TSettings, TInstanceSettings>()` — config-driven provider registration helper called by L5 packages from inside their `Add{Source}<T>(key)` extensions; mirrors the AuthZ/Identity track helpers.

**Deferred-mapping record:**

- `InvocationProviderMapping(string ProviderName, Action<IEndpointRouteBuilder> Map)` — per-provider endpoint-mapping closure stashed as a DI singleton during the services phase. Resolved by L5 Runtime Extensions packages at their own `Map*Invocation()` time — the umbrella `Cirreum.Runtime.Invocation` invokes all of them; per-source packages (e.g., `Cirreum.Runtime.Invocation.SignalR`) filter by `ProviderName` and invoke only theirs.

**Standardized `Items`-slot access (in `Cirreum.Security` namespace):**

- `InvocationContextAuthenticationExtensions` — typed `Get*`/`Set*` extension methods wrapping `AuthenticationContextKeys` slots on `IInvocationContext`. Consumers at L4+ depend on these in preference to direct dictionary access against the key constants. (L3 framework code uses raw access for the few centralized reads/writes it does.)

### Architecture position

This package is the **L4 Runtime** counterpart to the L2 `Cirreum.InvocationProvider` abstractions. Apps with only HTTP invocations don't reference it (the HTTP path is handled by `Cirreum.Services.Server` directly). Apps that use long-lived invocation sources (SignalR, WebSocket) get this package transitively through their L5 source package (e.g., `Cirreum.Runtime.Invocation.SignalR`).

No intra-layer references — `Cirreum.Runtime.Server` does not reference this package; the L5 source packages reference both their L3 per-source base AND this L4 runtime helper.

The endpoints-phase mapping (`MapInvocation()` / per-source `Map*Invocation()`) deliberately lives at L5, not here, so each per-source package can compose its mapping under a coherent name alongside ASP.NET's matching primitives (`MapHub<THub>()` etc.). This package only stashes the closures — it doesn't dictate how or when they fire.
