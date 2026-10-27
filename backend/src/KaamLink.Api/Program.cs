using KaamLink.Api.Configuration;
using KaamLink.Api.Middleware;
using KaamLink.Application;
using KaamLink.Application.Options;
using KaamLink.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Optional untracked local overrides for developer machines (gitignored).
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false);

// Options: bind + validate up-front so misconfiguration fails at startup, not at first request.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var otpOptions = builder.Configuration.GetSection(OtpOptions.SectionName).Get<OtpOptions>() ?? new OtpOptions();
var adminOptions = builder.Configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>() ?? new AdminOptions();
var supabaseOptions = builder.Configuration.GetSection(SupabaseOptions.SectionName).Get<SupabaseOptions>() ?? new SupabaseOptions();
StartupGuards.ValidateOptions(jwtOptions, otpOptions, supabaseOptions);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(otpOptions);
builder.Services.AddSingleton(adminOptions);
builder.Services.AddSingleton(supabaseOptions);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(jwtOptions);

var app = builder.Build();

// Environment rules (Production hardening: no sandbox OTP provider, no default DB credentials).
StartupGuards.ValidateEnvironment(app.Environment, app.Configuration);

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Extracts the (normalized) phone from OTP request bodies so rate limiting can
// partition per target phone, with IP fallback. Runs before the limiter.
app.UseMiddleware<PhoneExtractionMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

await StartupTasks.ApplyMigrationsOnStartupAsync(app);
await StartupTasks.EnsureReferenceDataSeededAsync(app);

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program
{
}
