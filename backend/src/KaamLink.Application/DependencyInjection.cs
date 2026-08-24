using KaamLink.Application.Auth;
using KaamLink.Application.Companies;
using KaamLink.Application.Jobs;
using KaamLink.Application.Skills;
using KaamLink.Application.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KaamLink.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers application use-case services. Options (OtpOptions/JwtOptions/AdminOptions)
    /// are bound and validated by the Api host before this call; TimeProvider defaults
    /// to the system clock and is replaced by fakes in tests.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWorkerProfileService, WorkerProfileService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ISkillCatalogService, SkillCatalogService>();
        services.AddScoped<IVacancyService, VacancyService>();
        services.AddScoped<IJobApplicationService, JobApplicationService>();
        return services;
    }
}
