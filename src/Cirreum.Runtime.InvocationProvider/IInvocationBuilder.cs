namespace Cirreum.Invocation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Fluent configuration builder for the Cirreum Invocation family. Passed into the
/// callback argument of the host-runtime layer's <c>AddInvocation(configure)</c>
/// extension. L5 invocation-source packages surface their app-facing extension methods
/// (<c>AddSignalR&lt;THub&gt;</c>, <c>AddWebSocket&lt;THandler&gt;</c>, future
/// <c>AddGrpc&lt;TService&gt;</c>) on this interface.
/// </summary>
/// <remarks>
/// <para>
/// The builder scopes per-instance source registration to a clear site in
/// <c>Program.cs</c>:
/// </para>
/// <code>
/// builder.AddInvocation(b =&gt; b
///     .AddSignalR&lt;ChatHub&gt;("chat")
///     .AddSignalR&lt;NotificationHub&gt;("notifications")
///     .AddWebSocket&lt;VoiceFrameHandler&gt;("voice"));
/// </code>
/// <para>
/// For advanced scenarios, consumers can access the underlying
/// <see cref="IHostApplicationBuilder"/> via <see cref="HostBuilder"/>, the bound
/// <see cref="IServiceCollection"/> via <see cref="Services"/>, or the root
/// <see cref="IConfiguration"/> via <see cref="Configuration"/>.
/// </para>
/// </remarks>
public interface IInvocationBuilder {

	/// <summary>Gets the underlying host application builder.</summary>
	IHostApplicationBuilder HostBuilder { get; }

	/// <summary>Gets the bound service collection.</summary>
	IServiceCollection Services { get; }

	/// <summary>Gets the root configuration object.</summary>
	IConfiguration Configuration { get; }

	/// <summary>The set of registered <c>{ProviderName}::{instanceKey}</c> pairs (diagnostic).</summary>
	IReadOnlyCollection<string> RegisteredInstances { get; }

	/// <summary>
	/// Records that an instance of an invocation source has been registered. Throws if the
	/// same <c>{ProviderName}::{instanceKey}</c> pair is registered twice within a single
	/// <c>AddInvocation(...)</c> call.
	/// </summary>
	void TrackInstance(string providerName, string instanceKey);

}
