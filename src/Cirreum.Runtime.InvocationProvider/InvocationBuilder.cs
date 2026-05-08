namespace Cirreum.Invocation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Default <see cref="IInvocationBuilder"/> implementation. Instantiated by the
/// host-runtime layer inside <c>AddInvocation(configure)</c> and passed to the caller's
/// configuration callback.
/// </summary>
public sealed class InvocationBuilder(IHostApplicationBuilder hostBuilder) : IInvocationBuilder {

	private readonly HashSet<string> _registeredInstances = [];

	/// <inheritdoc />
	public IHostApplicationBuilder HostBuilder => hostBuilder;

	/// <inheritdoc />
	public IServiceCollection Services => hostBuilder.Services;

	/// <inheritdoc />
	public IConfiguration Configuration => hostBuilder.Configuration;

	/// <inheritdoc />
	public IReadOnlyCollection<string> RegisteredInstances => this._registeredInstances;

	/// <inheritdoc />
	public void TrackInstance(string providerName, string instanceKey) {
		ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
		ArgumentException.ThrowIfNullOrWhiteSpace(instanceKey);

		var trackingKey = $"{providerName}::{instanceKey}";
		if (!this._registeredInstances.Add(trackingKey)) {
			throw new InvalidOperationException(
				$"An invocation source instance with key '{instanceKey}' for provider '{providerName}' " +
				$"has already been registered in this AddInvocation(...) call.");
		}
	}

}
