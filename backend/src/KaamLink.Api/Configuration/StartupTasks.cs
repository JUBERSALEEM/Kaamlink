using KaamLink.Infrastructure.Persistence;
using KaamLink.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Api.Configuration;

public static class StartupTasks
{
    /// <summary>
    /// Applies EF Core migrations on startup when enabled (Database:ApplyMigrationsOnStartup).
    /// Default: Development/Staging = true (disposable databases), Production = false
    /// (migrations are an explicit deployment step there).
    /// </summary>
    public static async Task ApplyMigrationsOnStartupAsync(WebApplication app)
    {
        var enabled = app.Configuration.GetValue(
            "Database:ApplyMigrationsOnStartup",
            app.Environment.IsDevelopment() || app.Environment.IsStaging());

        if (!enabled)
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        app.Logger.LogInformation(
            "Database migrations applied on startup (environment: {Environment}).",
            app.Environment.EnvironmentName);
    }

    /// <summary>
    /// Idempotent reference-data seed (skill catalog). Runs in every environment —
    /// fixed GUIDs make it a safe no-op when data already exists.
    /// </summary>
    public static async Task EnsureReferenceDataSeededAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SkillCatalogSeeder.EnsureSeededAsync(db, app.Logger);
        await AssessmentCatalogSeeder.EnsureSeededAsync(db, app.Logger);
    }
}
