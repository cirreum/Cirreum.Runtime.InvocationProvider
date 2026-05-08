namespace Microsoft.Extensions.Hosting;

using Cirreum.Invocation;
using Cirreum.Invocation.Configuration;
using Cirreum.Logging.Deferred;
using Cirreum.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Config-driven registration helpers for the Cirreum Invocation provider family.
/// </summary>
public static class HostApplicationBuilderExtensions {

	/// <summary>
	/// Register an Invocation provider's registrar: instantiates <typeparamref name="TRegistrar"/>,
	/// binds its settings from <c>Cirreum:Invocation:Providers:{ProviderName}</c>, runs the
	/// registrar's services-phase registration, and stashes an <see cref="InvocationProviderMapping"/>
	/// in DI so the host runtime can invoke the endpoints-phase registration after
	/// <c>builder.Build()</c> via <c>app.MapInvocation()</c>.
	/// </summary>
	/// <typeparam name="TRegistrar">The invocation provider registrar type.</typeparam>
	/// <typeparam name="TSettings">The provider settings type.</typeparam>
	/// <typeparam name="TInstanceSettings">The provider instance settings type.</typeparam>
	/// <param name="builder">The host application builder.</param>
	/// <param name="required">If <see langword="true"/>, throws when the configuration section is missing. Defaults to <see langword="false"/>.</param>
	/// <returns>The host application builder for chaining.</returns>
	/// <remarks>
	/// Called by L5 invocation-source extension methods (<c>AddSignalR&lt;THub&gt;</c>,
	/// <c>AddWebSocket&lt;THandler&gt;</c>, etc.) inside their <see cref="IInvocationBuilder"/>
	/// extension on the underlying <see cref="IHostApplicationBuilder"/>. App code does not
	/// normally call this directly.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="required"/> is <see langword="true"/> and the configuration
	/// section is missing, or when the section exists but cannot be bound to
	/// <typeparamref name="TSettings"/>.
	/// </exception>
	public static IHostApplicationBuilder RegisterInvocationProvider<TRegistrar, TSettings, TInstanceSettings>(
		this IHostApplicationBuilder builder,
		bool required = false)
		where TRegistrar : InvocationProviderRegistrar<TSettings, TInstanceSettings>, new()
		where TSettings : InvocationProviderSettings<TInstanceSettings>
		where TInstanceSettings : InvocationProviderInstanceSettings {

		var registrarName = typeof(TRegistrar).Name;
		var deferredLogger = Logger.CreateDeferredLogger();

		using (var loggingScope = deferredLogger.BeginScope(new { RegistrarName = registrarName })) {

			// Dedup: if this registrar type has already been wired up, skip.
			if (builder.Services.IsMarkerTypeRegistered<TRegistrar>()) {
				deferredLogger.LogDebug(
					"Duplicate request for {RegistrarName} and will be skipped.",
					registrarName);
				return builder;
			}

			builder.Services.MarkTypeAsRegistered<TRegistrar>();

			var registrar = new TRegistrar();
			var providerSectionKey = GetProviderConfigPath(registrar.ProviderType, registrar.ProviderName);
			var providerSection = builder.Configuration.GetSection(providerSectionKey);
			if (!providerSection.Exists()) {
				if (required) {
					throw new InvalidOperationException(
						$"Configuration required but not found for '{registrarName}' at '{providerSectionKey}'.");
				}

				deferredLogger.LogDebug(
					"Skipping '{RegistrarName}' — no configuration found at '{ConfigPath}'.",
					registrarName,
					providerSectionKey);
				return builder;
			}

			var providerSettings = providerSection.Get<TSettings>()
				?? throw new InvalidOperationException(
					$"Invalid configuration for '{registrarName}' — section exists but cannot be bound to settings.");

			if (providerSettings.Instances.Count == 0) {
				deferredLogger.LogWarning(
					"No instances found to register for {RegistrarName}.",
					registrarName);
				return builder;
			}

			// Services-phase registration: let the registrar wire up per-instance DI
			// (HubFilter, frame handlers, gRPC interceptors, etc.) using the bound settings.
			registrar.Register(
				providerSettings,
				builder.Services,
				builder.Configuration);

			// Endpoints-phase registration is deferred — IEndpointRouteBuilder isn't
			// available until after builder.Build(). Stash a closure that
			// MapInvocation() can invoke later.
			builder.Services.AddSingleton(new InvocationProviderMapping(
				registrar.ProviderName,
				endpoints => registrar.Map(providerSettings, endpoints)));

			deferredLogger.LogDebug(
				"Registered {InstanceCount} provider instance(s) for {RegistrarName} of type {ProviderType}.",
				providerSettings.Instances.Count,
				registrarName,
				registrar.ProviderType);
		}

		return builder;
	}

	// Helper method for building provider configuration paths.
	private static string GetProviderConfigPath(ProviderType providerType, string providerName) =>
		$"Cirreum:{providerType}:Providers:{providerName}";
}
