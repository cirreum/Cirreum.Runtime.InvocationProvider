# Cirreum.Runtime.InvocationProvider 1.2.0 — `InvocationContextAuthenticationExtensions` removed (clarifying the contract)

Removes the speculative typed-extension surface (`GetAuthenticatedScheme` / `SetAuthenticatedScheme` / `GetApplicationUserCache` / `SetApplicationUserCache`) on `IInvocationContext`. These extensions were added in 1.0.0 on the premise that consumers might want type-safe access to the well-known auth `Items`-slot keys. On review, that premise was wrong: **app code should not be reading or writing these slots at all** — they're framework-internal cache state. Removing the surface clarifies the contract and eliminates an attractive nuisance.

Ships alongside the auth-slot flow-through fixes in `Cirreum.Invocation.SignalR 1.2.1` / `Cirreum.Invocation.WebSockets 1.2.1` / `Cirreum.Services.Server` patch — same audit cycle that revealed the architectural intent these typed extensions misrepresented.

---

## Why this release exists

The auth-slot keys (`AuthenticationContextKeys.AuthenticatedScheme` and `AuthenticationContextKeys.ApplicationUserCache`) were always framework-internal:

- **Writers**: `AudienceProviderRoleClaimsTransformer` (the audience-auth claims-transformer), the dynamic-scheme forward selector in `Cirreum.Runtime.Authorization`, `UserStateAccessor.ResolveApplicationUserAsync` — all framework code, all using raw dictionary access against the const string keys.
- **Readers**: `UserStateAccessor` (the canonical reader), and now (post-1.2.1) the `SeedAuthSlots` helpers in `SignalRInvocationContext` / `WebSocketInvocationContext`. All framework code, all using raw dictionary access.

App code consumes `IUserStateAccessor.GetUser()` → `IUserState` (the canonical app-facing API), which internally reads the cache. App code has no legitimate reason to read or write the auth slots directly.

The 1.0.0-era reasoning for the typed extensions was *"prevent SCATTERED dictionary access at consumer call sites at L4+ and in app code."* But **there are no consumer call sites at L4+ or in app code** — only framework writers and readers, all using raw access by convention. The typed extensions were dead code that was also a subtle attractive nuisance: an app developer skimming the API surface might find them, assume "this is the sanctioned path for app code to read/write the auth cache," and start fiddling with framework-internal state.

Removing the surface clarifies the contract: **there is no public typed API to read or write the auth `Items` slots, because app code shouldn't.**

---

## What's changed

### `InvocationContextAuthenticationExtensions` removed

The static class at `Cirreum.Security.InvocationContextAuthenticationExtensions` is deleted. All four extension methods on `IInvocationContext` are gone:

- `GetAuthenticatedScheme()` — removed
- `SetAuthenticatedScheme(string)` — removed
- `GetApplicationUserCache()` — removed
- `SetApplicationUserCache(IApplicationUser)` — removed

The empty `Security/` folder under the package source is removed.

### Why this is 1.2.0 and not 2.0.0

Strictly per SemVer, removing a public type is a breaking change. Calling this 1.2.0 (Minor) under the same window-of-no-consumers, framework-owned-implementer-set precedent as the 1.1.0 (`OnDisconnectedAsync` signature change), 1.2.0 of `Cirreum.InvocationProvider` (`Abort()` interface widening), and 1.3.0 of `Cirreum.InvocationProvider` (`IConnectionSender` consolidation) cascades:

- **Zero consumers exist.** Verified by codebase grep — these methods are defined nowhere else than their own file. No app, no framework code calls them.
- **The intent was always framework-internal.** Removing the surface aligns the API with the actual contract.
- **Bumping to 2.0.0 would overstate the impact** — `MIGRATION-v2.md` ceremony, suggesting to readers that something fundamental changed. Nothing fundamental changed; one speculative surface that turned out to be an attractive nuisance was removed.

If any external consumer surfaces (none known), the migration is trivial: replace `invocation.GetAuthenticatedScheme()` → `invocation.Items[AuthenticationContextKeys.AuthenticatedScheme] as string` and similar for the other three. But again — app code shouldn't be doing this; the canonical path is `IUserStateAccessor.GetUser()`.

---

## Coordinated work

Ships alongside the broader Piece-1 auth-slot flow-through audit:

- **`Cirreum.Invocation.SignalR 1.2.1`** — copies auth slots from upgrade-time `HttpContext.Items` onto `SignalRConnection.Items`; `SignalRInvocationContext` seeds per-invocation `Items` from `Connection.Items` at construction.
- **`Cirreum.Invocation.WebSockets 1.2.1`** — same pattern for the WebSocket adapter (`WebSocketOrchestrator` + `WebSocketInvocationContext`).
- **`Cirreum.Services.Server` patch** — `UserStateAccessor` double-writes the resolved `IApplicationUser` to both per-invocation `Items` AND `invocation.Connection?.Items` on lazy resolve. Future-proofs the AI/LLM Piece 2 seam (null-scheme resolvers).

---

## Compatibility

- **Source-incompatible** for any external code calling the four removed extensions (zero known consumers).
- **Source- and binary-compatible** for everything else in this package's public surface (`IInvocationBuilder`, `InvocationBuilder`, `InvocationProviderMapping`, `AddInvocation` / `RegisterInvocationProvider` extensions, `Map*Invocation` extensions). All other types and methods are unchanged.

---

## See also

- `CHANGELOG.md` — condensed change list.
- [`Cirreum.Invocation.SignalR 1.2.1`](https://www.nuget.org/packages/Cirreum.Invocation.SignalR) — coordinated adapter fix.
- [`Cirreum.Invocation.WebSockets 1.2.1`](https://www.nuget.org/packages/Cirreum.Invocation.WebSockets) — coordinated adapter fix.
- [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md) — the foundational invocation-context seam.
- `RELEASE-NOTES-v1.0.0.md` — original release that introduced the typed extensions (now retired).
