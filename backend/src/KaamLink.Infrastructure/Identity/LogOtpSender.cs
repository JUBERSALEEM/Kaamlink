using KaamLink.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace KaamLink.Infrastructure.Identity;

/// <summary>
/// Development sandbox OTP "provider": writes the code to application logs instead
/// of sending an SMS. Enabled only when Otp:Provider == "log"; Production startup
/// guards reject that value so this can never be the live behavior.
/// </summary>
public sealed class LogOtpSender(ILogger<LogOtpSender> logger) : IOtpSender
{
    public Task SendAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        logger.LogWarning(
            "[OTP SANDBOX] Code for {PhoneNumber} is {Code} " +
            "(Otp:Provider=log — development only, replace before any deployed environment).",
            phoneNumber,
            code);

        return Task.CompletedTask;
    }
}
