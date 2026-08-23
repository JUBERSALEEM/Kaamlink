namespace KaamLink.Application.Abstractions;

/// <summary>
/// Delivery channel for OTP codes (§35 provider abstraction). Implementations:
/// LogOtpSender (development sandbox) today; real SMS provider later. Business
/// logic must never depend on a concrete provider.
/// </summary>
public interface IOtpSender
{
    Task SendAsync(string phoneNumber, string code, CancellationToken ct = default);
}
