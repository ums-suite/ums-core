using Microsoft.Extensions.DependencyInjection;

namespace UMS.Shared.Resilience.Http;

/// <summary>
/// Every external call (payment gateway, SMS/email provider, object storage) goes through a named
/// or typed <see cref="HttpClient"/> registered with this instead of a bare
/// <c>services.AddHttpClient()</c> (ums-conventions.md, Resilience & Reliability: "never hand-rolled
/// per integration"). <c>AddStandardResilienceHandler</c> wires retry with exponential backoff +
/// jitter, a circuit breaker, and a timeout in one call.
/// </summary>
public static class ResilientHttpClientExtensions
{
    public static IHttpClientBuilder AddUmsResilientHttpClient(this IServiceCollection services, string name)
    {
        var builder = services.AddHttpClient(name);
        builder.AddStandardResilienceHandler();
        return builder;
    }

    public static IHttpClientBuilder AddUmsResilientHttpClient<TClient>(this IServiceCollection services)
        where TClient : class
    {
        var builder = services.AddHttpClient<TClient>();
        builder.AddStandardResilienceHandler();
        return builder;
    }
}
