namespace Cirreum.Security;

using Cirreum.Invocation;

/// <summary>
/// Typed extensions for authentication-related slots in
/// <see cref="IInvocationContext.Items"/>. Consumers should use these in preference to
/// direct dictionary access against <see cref="AuthenticationContextKeys"/> — the keys
/// are framework implementation detail; the typed extensions are the public surface.
/// </summary>
/// <remarks>
/// <para>
/// New well-known slots are added by defining a key constant on
/// <see cref="AuthenticationContextKeys"/> and a paired <c>Get*</c>/<c>Set*</c> extension
/// here. Other concern domains (correlation, audit, tenant, etc.) get their own
/// <c>InvocationContext*Extensions</c> class to keep the surface organized.
/// </para>
/// <para>
/// L3 framework code (<c>UserAccessor</c>, the role claims transformer, etc.) generally
/// uses raw dictionary access for the few centralized reads/writes it does — the typed
/// extensions exist to prevent SCATTERED dictionary access across many consumer call
/// sites at L4+ and in app code.
/// </para>
/// </remarks>
public static class InvocationContextAuthenticationExtensions {

	/// <summary>
	/// Gets the authentication scheme that authenticated this invocation, stamped during
	/// dynamic scheme dispatch by the forward selector and defensively by the role claims
	/// transformer.
	/// </summary>
	/// <returns>The scheme name, or <see langword="null"/> if not stamped.</returns>
	public static string? GetAuthenticatedScheme(this IInvocationContext invocation) {
		ArgumentNullException.ThrowIfNull(invocation);
		return invocation.Items.TryGetValue(AuthenticationContextKeys.AuthenticatedScheme, out var value)
			? value as string
			: null;
	}

	/// <summary>
	/// Sets the authentication scheme that authenticated this invocation. Called by the
	/// forward selector / role claims transformer; not normally called from app code.
	/// </summary>
	public static void SetAuthenticatedScheme(this IInvocationContext invocation, string scheme) {
		ArgumentNullException.ThrowIfNull(invocation);
		ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
		invocation.Items[AuthenticationContextKeys.AuthenticatedScheme] = scheme;
	}

	/// <summary>
	/// Gets the resolved <see cref="IApplicationUser"/> cached during role enrichment,
	/// avoiding a redundant resolver call in <c>UserAccessor</c>.
	/// </summary>
	/// <returns>The cached application user, or <see langword="null"/> if not cached.</returns>
	public static IApplicationUser? GetApplicationUserCache(this IInvocationContext invocation) {
		ArgumentNullException.ThrowIfNull(invocation);
		return invocation.Items.TryGetValue(AuthenticationContextKeys.ApplicationUserCache, out var value)
			? value as IApplicationUser
			: null;
	}

	/// <summary>
	/// Sets the resolved <see cref="IApplicationUser"/> in the per-invocation cache so
	/// downstream consumers (e.g., <c>UserAccessor</c>) can read it without re-resolving.
	/// </summary>
	public static void SetApplicationUserCache(this IInvocationContext invocation, IApplicationUser user) {
		ArgumentNullException.ThrowIfNull(invocation);
		ArgumentNullException.ThrowIfNull(user);
		invocation.Items[AuthenticationContextKeys.ApplicationUserCache] = user;
	}

}
