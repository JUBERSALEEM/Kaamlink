using KaamLink.Api.Configuration;
using KaamLink.Application.Abstractions;
using KaamLink.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KaamLink.Api.Controllers;

/// <summary>
/// Phone-OTP authentication endpoints. Access tokens are short-lived (15 min
/// default); clients renew them via the rotating refresh-token flow.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Request an OTP code for a phone number. Rate-limited per target phone.</summary>
    [HttpPost("otp/request")]
    [EnableRateLimiting(RateLimitPolicies.OtpRequest)]
    public async Task<ActionResult<RequestOtpResponse>> RequestOtp(
        [FromBody] RequestOtpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await authService.RequestOtpAsync(request, cancellationToken));

    /// <summary>Verify a code. Registers the account on first success and issues a token pair.</summary>
    [HttpPost("otp/verify")]
    [EnableRateLimiting(RateLimitPolicies.OtpVerify)]
    public async Task<ActionResult<AuthTokensResponse>> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await authService.VerifyOtpAsync(request, cancellationToken));

    /// <summary>
    /// Exchange a verified Supabase email OTP/password session token for KaamLink's own
    /// access/refresh token pair. The token is validated server-side with GoTrue.
    /// </summary>
    [HttpPost("email/exchange")]
    [EnableRateLimiting(RateLimitPolicies.OtpVerify)]
    public async Task<ActionResult<AuthTokensResponse>> ExchangeSupabaseToken(
        [FromBody] SupabaseExchangeRequest request,
        CancellationToken cancellationToken) =>
        Ok(await authService.ExchangeSupabaseTokenAsync(request, cancellationToken));

    /// <summary>Rotate a refresh token into a new token pair. Presenting a rotated token revokes the session family.</summary>
    [HttpPost("token/refresh")]
    public async Task<ActionResult<AuthTokensResponse>> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken) =>
        Ok(await authService.RefreshAsync(request, cancellationToken));

    /// <summary>Revoke a refresh token belonging to the caller (session sign-out).</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(currentUser.UserId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>The caller's identity — the canonical "who am I" probe for clients.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserSummary>> Me(CancellationToken cancellationToken) =>
        Ok(await authService.GetMeAsync(currentUser.UserId, cancellationToken));
}
