using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace UMS.Shared.ErrorHandling;

/// <summary>One shared DI registration per deployable - see GlobalExceptionHandler's own remarks.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddUmsErrorHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }

    public static WebApplication UseUmsErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        return app;
    }
}
