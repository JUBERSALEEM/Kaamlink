using KaamLink.Features.Employer;
using KaamLink.Features.Onboarding;
using KaamLink.Features.Worker;
using Microsoft.Extensions.Logging;

namespace KaamLink
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            RegisterServices(builder.Services);

            return builder.Build();
        }

        /// <summary>
        /// Dependency injection registrations. Pages and view models are currently
        /// the only services; repositories/API clients/sync services are added in
        /// later phases behind interfaces (see docs/phase-01-report.md, section E).
        /// </summary>
        private static void RegisterServices(IServiceCollection services)
        {
            // Entry point
            services.AddTransient<RoleSelectionPage>();
            services.AddTransient<RoleSelectionViewModel>();

            // Worker experience root
            services.AddTransient<WorkerHomePage>();
            services.AddTransient<WorkerHomeViewModel>();

            // Employer experience root
            services.AddTransient<EmployerHomePage>();
            services.AddTransient<EmployerHomeViewModel>();
        }
    }
}
