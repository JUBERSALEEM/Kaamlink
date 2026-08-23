using KaamLink.Application.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KaamLink.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers application use-case services. Options (OtpOptions/JwtOptions) are
    /// bound and validated by the Api host before this call; TimeProvider defaults
    /// to the system clock and is replaced by fakes in tests.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
