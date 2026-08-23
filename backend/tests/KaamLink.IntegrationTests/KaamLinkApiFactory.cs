using KaamLink.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace KaamLink.IntegrationTests;

/// <summary>
/// Boots the real API in-memory against a real PostgreSQL (Testcontainers).
/// Configuration and the OTP sender are overridden; production startup guards
/// remain intact because the host runs in the Development environment.
/// </summary>
public sealed class KaamLinkApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("kaamlink_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public CapturingOtpSender OtpSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Auth:Jwt:SigningKey"] = "integration-tests-signing-key-0123456789",
                ["Otp:HashingKey"] = "integration-tests-hashing-key-0123",
                ["Otp:Provider"] = "log",
                // Tests exercise the HTTP rate limiter separately; the domain resend
                // cooldown (60s) would only add wall-clock sleeps here.
                ["Otp:ResendCooldownSeconds"] = "0"
            }));

        builder.ConfigureServices(services =>
        {
            // Last registration wins: tests capture codes instead of "sending" them.
            services.AddSingleton<IOtpSender>(OtpSender);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Force host startup (runs migrations on startup in Development).
        _ = CreateClient();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
