namespace Cirreum.Invocation;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Default <see cref="IInvocationBuilder"/> implementation. Instantiated by L5 invocation-source
/// extensions inside their <c>Add{Source}Invocation(configure)</c> entry points and passed to
/// the caller's configuration callback.
/// </summary>
public sealed class InvocationBuilder(IHostApplicationBuilder hostBuilder) : IInvocationBuilder {

	/// <inheritdoc />
	public IHostApplicationBuilder HostBuilder => hostBuilder;

}
