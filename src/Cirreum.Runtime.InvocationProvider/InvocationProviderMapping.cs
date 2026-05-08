namespace Cirreum.Invocation;

using Microsoft.AspNetCore.Routing;

/// <summary>
/// Per-provider endpoint-mapping delegate stashed in DI during
/// <c>RegisterInvocationProvider&lt;,,&gt;()</c> (the services phase) for deferred
/// execution by <c>app.MapInvocation()</c> in the host runtime layer (the
/// endpoints phase, after <c>builder.Build()</c>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ProviderName"/> is the registrar's <c>ProviderName</c> (e.g.
/// <c>"SignalR"</c>, <c>"WebSocket"</c>). Consumers that only want to map a single
/// invocation source can filter the <see cref="IEnumerable{T}"/> resolved from DI on
/// this property; <c>MapInvocation()</c> invokes every registered mapping.
/// </para>
/// <para>
/// <see cref="Map"/> is a closure over the registrar instance and its bound provider
/// settings — calling it on an <see cref="IEndpointRouteBuilder"/> walks the enabled
/// instances and maps each one's endpoint(s).
/// </para>
/// </remarks>
/// <param name="ProviderName">The registrar's <c>ProviderName</c>.</param>
/// <param name="Map">Deferred endpoint-mapping delegate.</param>
public sealed record InvocationProviderMapping(
	string ProviderName,
	Action<IEndpointRouteBuilder> Map);
