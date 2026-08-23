using KaamLink.Infrastructure.Persistence;
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
}
