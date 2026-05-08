namespace Cirreum.Invocation;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Fluent configuration builder for the Cirreum Invocation family. Created by L5 invocation-source
/// extensions (<c>AddSignalRInvocation</c>, <c>AddWebSocketInvocation</c>) and the umbrella
/// <c>AddInvocation</c>; passed to the caller's <c>configure</c> callback so per-instance
/// extension methods (<c>AddSignalR&lt;THub&gt;</c>, <c>AddWebSocket&lt;THandler&gt;</c>, …) can
/// be chained for each invocation source the app uses.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <c>IIdentityBuilder</c>'s minimalism — the builder's only job is to scope per-source
/// extension methods to a clear site in <c>Program.cs</c>:
/// </para>
/// <code>
/// builder.AddInvocation(b =&gt; b
///     .AddSignalR&lt;ChatHub&gt;("chat")
///     .AddWebSocket&lt;VoiceFrameHandler&gt;("voice"));
/// </code>
/// <para>
/// Per-source extension methods (the <c>AddSignalR&lt;THub&gt;</c>, <c>AddWebSocket&lt;TH&gt;</c>
/// pattern) live in their respective L5 Runtime Extensions packages, attached to this interface.
/// L4 doesn't bake source-specific knowledge into the builder.
/// </para>
/// </remarks>
public interface IInvocationBuilder {

	/// <summary>Gets the underlying host application builder.</summary>
	IHostApplicationBuilder HostBuilder { get; }

}
