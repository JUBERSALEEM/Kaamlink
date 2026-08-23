using System.Text;
using System.Threading.RateLimiting;
using KaamLink.Api.Health;
using KaamLink.Api.Middleware;
using KaamLink.Api.Services;
using KaamLink.Application.Abstractions;
using KaamLink.Application.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

namespace KaamLink.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    /// <summary>Host-layer services: current-user resolution, JWT auth, authorization, health checks, rate limiting.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, JwtOptions jwtOptions)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddScoped<ITokenService, JwtTokenService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep "sub"/custom claims as issued instead of remapping to WS-* URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub"
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.PlatformAdmin,
                policy => policy.RequireAuthenticatedUser().RequireClaim("platform_admin", "true"));
        });

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Live."), tags: ["live"])
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = RateLimitRejection.WriteProblemDetailsAsync;

            options.AddPolicy(
                RateLimitPolicies.OtpRequest,
                context => RateLimitPartition.GetSlidingWindowLimiter(
                    RateLimitKey.For(context),
                    _ => new SlidingWindowLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(5),
                        SegmentsPerWindow = 5,
                        QueueLimit = 0
                    }));

            options.AddPolicy(
                RateLimitPolicies.OtpVerify,
                context => RateLimitPartition.GetSlidingWindowLimiter(
                    RateLimitKey.For(context),
                    _ => new SlidingWindowLimiterOptions
                    {
                        PermitLimit = 15,
                        Window = TimeSpan.FromMinutes(5),
                        SegmentsPerWindow = 5,
                        QueueLimit = 0
                    }));
        });

        return services;
    }
}
