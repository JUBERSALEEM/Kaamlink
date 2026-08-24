using KaamLink.Application.Abstractions;
using KaamLink.Application.Options;
using KaamLink.Infrastructure.Auditing;
using KaamLink.Infrastructure.Companies;
using KaamLink.Infrastructure.Identity;
using KaamLink.Infrastructure.Persistence;
using KaamLink.Infrastructure.Skills;
using KaamLink.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KaamLink.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' is not configured. " +
                "Set ConnectionStrings__Postgres (see backend/.env.example).");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOtpChallengeRepository, OtpChallengeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditService, AuditService>();

        services.AddScoped<IWorkerProfileRepository, WorkerProfileRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ISkillCatalogRepository, SkillCatalogRepository>();
        services.AddScoped<IVacancyRepository, Jobs.VacancyRepository>();
        services.AddScoped<IJobApplicationRepository, Jobs.JobApplicationRepository>();

        // OTP provider selection behind the port (§35). "log" is the development
        // sandbox; Production startup guards reject it until a real provider is wired.
        services.AddSingleton<LogOtpSender>();
        services.AddSingleton<IOtpSender>(sp =>
        {
            var otp = sp.GetRequiredService<OtpOptions>();
            return otp.Provider.Equals("log", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<LogOtpSender>()
                : throw new InvalidOperationException(
                    $"Unknown OTP provider '{otp.Provider}'. Supported: log (development).");
        });

        return services;
    }
}
