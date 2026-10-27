using KaamLink.Core.Api;
using KaamLink.Core.Presentation.Theming;
using KaamLink.Features.Assessments;
using KaamLink.Features.Auth;
using KaamLink.Features.Employer;
using KaamLink.Features.Jobs;
using KaamLink.Features.Onboarding;
using KaamLink.Features.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace KaamLink
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Diagnostics: without this, a crash on a phone is just "the app closed".
            // Both lines land in logcat next to the transport-failure log, so
            // `adb logcat | grep KaamLink` shows the stack trace.
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Console.WriteLine($"[KaamLink] UNHANDLED EXCEPTION: {args.ExceptionObject}");

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Console.WriteLine($"[KaamLink] UNOBSERVED TASK EXCEPTION: {args.Exception}");
                args.SetObserved();
            };

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
                })
                .ConfigureLifecycleEvents(lifecycle =>
                {
#if ANDROID
                    // Android does not call Application.OnAppLinkRequestReceived
                    // automatically for a custom scheme. Forward both cold-start
                    // (OnCreate) and resumed-app (OnNewIntent) VIEW intents to App.
                    lifecycle.AddAndroid(android =>
                    {
                        android.OnCreate((activity, _) => ForwardAndroidAppLink(activity.Intent));
                        android.OnNewIntent((_, intent) => ForwardAndroidAppLink(intent));
                    });
#endif
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            RegisterServices(builder.Services);

            return builder.Build();
        }

#if ANDROID
        /// <summary>
        /// Only forward the known magic-link callback host. Other VIEW intents are
        /// ignored, so a malicious app cannot inject arbitrary URIs into auth.
        /// </summary>
        private static void ForwardAndroidAppLink(Android.Content.Intent? intent)
        {
            if (intent is null || intent.Action != Android.Content.Intent.ActionView)
                return;

            var data = intent.Data?.ToString();
            if (string.IsNullOrWhiteSpace(data)
                || !Uri.TryCreate(data, UriKind.Absolute, out var uri)
                || !EmailAuthLinkParser.IsCallback(uri))
                return;

            Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(uri);
        }
#endif

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

            // Core: session + api. Both HttpClients share ApiHttp.Configure, so a
            // stale BaseAddress or missing timeout cannot diverge between the API
            // and the silent-refresh client.
            services.AddSingleton<INetworkStatus, NetworkStatus>();
            services.AddSingleton<IEmailAuthLinkService, EmailAuthLinkService>();
            services.AddHttpClient<ISupabaseMagicLinkService, SupabaseMagicLinkService>(
                ApiHttp.ConfigureSupabase);
            services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
            services.AddHttpClient(ApiHttp.AuthClientName, ApiHttp.Configure);
            services.AddSingleton<SessionService>();
            services.AddSingleton<ISessionService>(sp => sp.GetRequiredService<SessionService>());
            services.AddTransient<AuthHeaderHandler>();
            services
                .AddHttpClient<IApiClient, ApiClient>(ApiHttp.Configure)
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
