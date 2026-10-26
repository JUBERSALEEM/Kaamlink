using KaamLink.Core.Api;
using KaamLink.Core.Presentation.Theming;
using KaamLink.Features.Assessments;
using KaamLink.Features.Auth;
using KaamLink.Features.Employer;
using KaamLink.Features.Jobs;
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
                    // Icon glyphs (Lucide, ISC licence — Resources/Fonts/lucide-LICENSE.txt).
                    // Single codepoint per icon; keys live in Icons.xaml (IconXxx) so a
                    // page never inlines a private-use glyph.
                    fonts.AddFont("lucide.ttf", "Lucide");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            RegisterServices(builder.Services);

            return builder.Build();
        }

        /// <summary>
        /// Composition root. Core services are singletons (session spans the whole
        /// app lifetime); pages and view models are transient (fresh state per visit).
        /// The api named client carries the bearer/refresh handler; the session
        /// service uses its own plain client so the graph cannot cycle.
        /// </summary>
        private static void RegisterServices(IServiceCollection services)
        {
            // Theme: one instance for the app lifetime; App applies the stored
            // palette before the shell is built, pages read it for the toggle.
            services.AddSingleton<IThemeService, ThemeService>();

            // Core: session + api
            services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
            services.AddSingleton<SessionService>();
            services.AddSingleton<ISessionService>(sp => sp.GetRequiredService<SessionService>());
            services.AddTransient<AuthHeaderHandler>();
            services
                .AddHttpClient<IApiClient, ApiClient>(client => client.BaseAddress = ApiConfig.BaseUri)
                .AddHttpMessageHandler<AuthHeaderHandler>();

            // Entry point
            services.AddTransient<RoleSelectionPage>();
            services.AddTransient<RoleSelectionViewModel>();

            // Auth
            services.AddTransient<LoginPage>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<OtpPage>();
            services.AddTransient<OtpViewModel>();

            // Worker experience
            services.AddTransient<WorkerOnboardingPage>();
            services.AddTransient<WorkerOnboardingViewModel>();
            services.AddTransient<WorkerHomePage>();
            services.AddTransient<WorkerHomeViewModel>();

            // Employer experience
            services.AddTransient<EmployerOnboardingPage>();
            services.AddTransient<EmployerOnboardingViewModel>();
            services.AddTransient<EmployerHomePage>();
            services.AddTransient<EmployerHomeViewModel>();

            // Skill assessments (Phase 05)
            services.AddTransient<MyAssessmentsPage>();
            services.AddTransient<MyAssessmentsViewModel>();
            services.AddTransient<AssessmentRunPage>();
            services.AddTransient<AssessmentRunViewModel>();

            // Jobs core (Phase 04)
            services.AddTransient<VacancyFeedPage>();
            services.AddTransient<VacancyFeedViewModel>();
            services.AddTransient<VacancyDetailPage>();
            services.AddTransient<VacancyDetailViewModel>();
            services.AddTransient<MyApplicationsPage>();
            services.AddTransient<MyApplicationsViewModel>();
            services.AddTransient<CompanyVacanciesPage>();
            services.AddTransient<CompanyVacanciesViewModel>();
            services.AddTransient<VacancyEditPage>();
            services.AddTransient<VacancyEditViewModel>();
            services.AddTransient<VacancyApplicationsPage>();
            services.AddTransient<VacancyApplicationsViewModel>();
        }
    }
}
