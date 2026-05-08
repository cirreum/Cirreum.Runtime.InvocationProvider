# Cirreum.Runtime.InvocationProvider 1.1.0 — Slim down `IInvocationBuilder`, move `AddInvocation` up to L5

Aligns the Invocation track's L4 surface with the Identity track's structurally — `IInvocationBuilder` is now just a scope object holding `HostBuilder`, and the app-facing `AddInvocation()` entry point moves to the L5 Runtime Extensions layer where it belongs (per-source `AddSignalRInvocation` / `AddWebSocketInvocation` / umbrella `AddInvocation`, mirroring `AddOidcIdentity` / `AddEntraExternalIdIdentity` / `AddIdentity`).

L2 floor bumps `1.0.1` → `1.1.0` to pick up `DisconnectInfo` and align with the L5 SignalR adapter.

Strictly source-incompatible with v1.0.0 in the cleanup areas. Same window-of-no-consumers reasoning that motivated v1.0.1's L2 corrections — no L5 Runtime Extensions packages have shipped that would consume v1.0.0's `AddInvocation` or the slimmed members.

---

## Why this release exists

When v1.0.0 shipped, `IInvocationBuilder` carried five public surface members (`HostBuilder`, `Services`, `Configuration`, `RegisteredInstances`, `TrackInstance`) and the package exposed an `AddInvocation()` entry point at L4. Comparison with the Identity track surfaced two cleanup opportunities:

1. **Three of the builder's members were redundant.** `Services` and `Configuration` were trivially reachable via `HostBuilder.Services` / `HostBuilder.Configuration`. `RegisteredInstances` and `TrackInstance` duplicated dedup work the L2 `InvocationProviderRegistrar<TSettings, TInstanceSettings>` base class was already doing more authoritatively via its static `ProcessedInstances` tracker. Identity's `IIdentityBuilder` ships with just `HostBuilder` (plus the `AddProvisioner<T>` method that's unique to Identity's per-instance side-input pattern) and works fine because the L2 registrar handles dedup.

2. **`AddInvocation` belonged at L5, not L4.** Identity's pattern places per-protocol entry points at L5 (`AddOidcIdentity` in `Cirreum.Runtime.Identity.Oidc`, `AddEntraExternalIdIdentity` in `Cirreum.Runtime.Identity.EntraExternalId`) and a unified umbrella entry at L5 (`AddIdentity` in `Cirreum.Runtime.Identity`). The Invocation track diverged: it placed a single `AddInvocation` at L4 with the expectation that L5 source packages would attach `AddSignalR<THub>` / `AddWebSocket<TH>` extensions to `IInvocationBuilder`. That worked but conflated layers — L4 was both the helper layer *and* the entry-point layer.

The fix is to delete what duplicates work the L2 registrar already does, delete what trivially reaches through `HostBuilder`, and let the L5 source packages own their own entry points.

---

## What changed

### `IInvocationBuilder` slimmed

```diff
public interface IInvocationBuilder {
    IHostApplicationBuilder HostBuilder { get; }
-   IServiceCollection Services { get; }
-   IConfiguration Configuration { get; }
-   IReadOnlyCollection<string> RegisteredInstances { get; }
-   void TrackInstance(string providerName, string instanceKey);
}
```

The `InvocationBuilder` class drops the corresponding `_registeredInstances` HashSet field and the property/method implementations. The constructor signature is unchanged.

### `AddInvocation` removed

The extension method on `IHostApplicationBuilder` is gone from this package. Its responsibility moves to the L5 Runtime Extensions layer:

```
L4 Cirreum.Runtime.InvocationProvider     ← THIS PACKAGE — IInvocationBuilder, RegisterInvocationProvider helper
L5 per-source Cirreum.Runtime.Invocation.SignalR     ← AddSignalRInvocation()
L5 per-source Cirreum.Runtime.Invocation.WebSockets  ← AddWebSocketInvocation()
L5 umbrella   Cirreum.Runtime.Invocation             ← AddInvocation() (calls each per-source)
```

This is the exact mirror of the Identity track and gives each invocation source its own discoverable entry point on `IHostApplicationBuilder`. Apps that only use SignalR install only the SignalR L5 package and call `AddSignalRInvocation()`; apps using multiple sources install the umbrella and call `AddInvocation()`.

### `RegisterInvocationProvider<TRegistrar, TSettings, TInstanceSettings>()` retained

The L4 helper that L5 source packages call internally is unchanged — it still binds the configuration section, runs the registrar's services-phase `Register(...)`, and stashes an `InvocationProviderMapping` in DI for the deferred endpoints phase. This is the L4's actual job and remains here.

### `InvocationProviderMapping` retained

Unchanged. The DI-stashed deferred-mapping record consumed by L5 source packages at their respective `Map*Invocation()` entry points.

---

## L2 floor bump

`Cirreum.InvocationProvider` `1.0.1` → `1.1.0`. Picks up the `DisconnectInfo` record and the updated `IConnectionLifecycle.OnDisconnectedAsync` signature shipped in L2 1.1.0. Aligns the L4 floor with the SignalR L3 adapter (`Cirreum.Invocation.SignalR 1.0.0`), which also floors at L2 1.1.0 — so the dependency graph is uniform.

---

## Migration from 1.0.0

For framework-internal consumers and L5 extension-authors:

| Before (1.0.0) | After (1.1.0) |
|---|---|
| `builder.Services` *(on IInvocationBuilder)* | `builder.HostBuilder.Services` |
| `builder.Configuration` *(on IInvocationBuilder)* | `builder.HostBuilder.Configuration` |
| `builder.RegisteredInstances` | (removed — L2 registrar's static `ProcessedInstances` does the dedup) |
| `builder.TrackInstance(name, key)` | (removed — same) |
| `app.AddInvocation(b => b.AddSignalR<THub>("key"))` | `app.AddSignalRInvocation(b => b.AddSignalR<THub>("key"))` *(per-source)* — or `app.AddInvocation(b => ...)` from the umbrella once it ships |

No published consumers of v1.0.0 are known to exist — the L5 Runtime Extensions packages that would consume `AddInvocation` haven't shipped yet, and `Cirreum.Runtime.Server` does not reference this package (no intra-layer L4 reference). Same window-of-no-consumers reasoning that motivated v1.0.1's L2 corrections.

---

## Why this is 1.1.0 and not 2.0.0

Strictly per SemVer, removing public extension methods and shrinking a public interface is a breaking change. We're calling this 1.1.0 (Minor) rather than 2.0.0 (Major) because:

- **Zero published consumers exist.** No L5 invocation-source package has shipped. `Cirreum.Runtime.Server` does not reference this package. App code in the wild that called `AddInvocation` would have nothing to do with the result yet because no per-source `AddSignalR<THub>` extensions exist on NuGet.
- **The cost of being slightly looser on SemVer here is bounded** (the breaking change can only affect a non-existent population), and the cost of bumping to 2.0.0 (`MIGRATION-v2.md` ceremony, suggesting to readers that something major changed) is real.

Same window-of-no-consumers reasoning that motivated `Cirreum.InvocationProvider 1.0.1`'s `IConnectionOutbound → IConnectionSender` rename and `1.1.0`'s `IConnectionLifecycle.OnDisconnectedAsync` signature change.

---

## Compatibility

- **Source-incompatible** with v1.0.0 in the cleanup areas (`AddInvocation` removed; four `IInvocationBuilder` members removed).
- **`HostBuilder` retained** — apps and L5 packages that only used this property continue to work unchanged.
- **`RegisterInvocationProvider<>`, `InvocationBuilder`, `InvocationProviderMapping`, the typed `Items`-slot extensions** — all unchanged.

---

## See also

- `CHANGELOG.md` — condensed change list for `1.1.0`.
- [`Cirreum.InvocationProvider 1.1.0`](https://www.nuget.org/packages/Cirreum.InvocationProvider) — L2 abstractions, floor for this release.
- [`Cirreum.Runtime.IdentityProvider`](https://www.nuget.org/packages/Cirreum.Runtime.IdentityProvider) — the structural template this release aligns with.
