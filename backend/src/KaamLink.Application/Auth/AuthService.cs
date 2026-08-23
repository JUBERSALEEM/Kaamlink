using System.Text.Json;
using KaamLink.Application.Abstractions;
using KaamLink.Application.Common;
using KaamLink.Application.Options;
using KaamLink.Domain.Auditing;
using KaamLink.Domain.Identity;
using KaamLink.Domain.Shared;

namespace KaamLink.Application.Auth;

/// <summary>
/// Phone-OTP authentication use cases:
/// request code → verify code (registers on first login) → refresh (rotating,
/// reuse-detecting) → logout (revoke) → me.
/// Save points are explicit (IUnitOfWork); audit events are write-through so
/// security-relevant failures are persisted even when the operation throws.
/// </summary>
public sealed class AuthService(
    IUserRepository users,
    IOtpChallengeRepository otpChallenges,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IOtpSender otpSender,
    ITokenService tokenService,
    IAuditService audit,
    OtpOptions otpOptions,
    JwtOptions jwtOptions,
    TimeProvider timeProvider) : IAuthService
{
    public async Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken ct = default)
    {
        var phone = ParsePhone(request.Phone);
        var now = timeProvider.GetUtcNow();

        var latest = await otpChallenges.FindLatestByPhoneAsync(phone.Value, ct);
        var cooldown = TimeSpan.FromSeconds(otpOptions.ResendCooldownSeconds);
        if (latest is not null && now - latest.CreatedAt < cooldown)
        {
            var retryAfter = (int)Math.Ceiling((cooldown - (now - latest.CreatedAt)).TotalSeconds);
            throw AppException.TooManyRequests(
                "auth.otp_resend_cooldown",
                "Please wait before requesting another code.",
                retryAfter);
        }

        var code = OtpCode.Generate();
        var challenge = OtpChallenge.Create(
            phone.Value,
            OtpHasher.Hash(code, otpOptions.HashingKey),
            now,
            TimeSpan.FromSeconds(otpOptions.ChallengeLifetimeSeconds));

        await otpChallenges.AddAsync(challenge, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await otpSender.SendAsync(phone.Value, code, ct);

        await audit.RecordAsync(
            AuditActions.OtpRequested,
            "otp_challenge",
            challenge.Id.ToString(),
            metadataJson: JsonSerializer.Serialize(new { phone = phone.Value, otpOptions.ChallengeLifetimeSeconds }),
            ct: ct);

        return new RequestOtpResponse(otpOptions.ResendCooldownSeconds, otpOptions.ChallengeLifetimeSeconds);
    }

    public async Task<AuthTokensResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default)
    {
        var phone = ParsePhone(request.Phone);
        var now = timeProvider.GetUtcNow();

        var challenge = await otpChallenges.FindLatestActiveByPhoneAsync(phone.Value, now, ct);
        if (challenge is null)
        {
            await RecordLoginFailureAsync(phone.Value, "no_active_or_unexpired_challenge", ct);
            throw AppException.BadRequest(
                "auth.otp_not_found_or_expired",
                "No active code for this number. Request a new one.");
        }

        if (!challenge.HasAttemptsRemaining)
        {
            await RecordLoginFailureAsync(phone.Value, "attempts_exhausted", ct);
            throw AppException.BadRequest(
                "auth.otp_locked",
                "Too many incorrect attempts. Request a new code.");
        }

        if (string.IsNullOrWhiteSpace(request.Code)
            || !OtpHasher.Verify(request.Code.Trim(), challenge.CodeHash, otpOptions.HashingKey))
        {
            challenge.RegisterFailedAttempt();
            await RecordLoginFailureAsync(phone.Value, "invalid_code", ct, challenge.AttemptsRemaining);
            throw AppException.BadRequest("auth.otp_invalid", "Incorrect code.");
        }

        var user = await users.FindByPhoneAsync(phone.Value, ct);
        if (user is not null && user.Status != UserStatus.Active)
        {
            await RecordLoginFailureAsync(
                phone.Value,
                user.Status == UserStatus.Blocked ? "account_blocked" : "account_deleted",
                ct);
            RejectUnavailableAccount(user);
        }

        challenge.Consume(now);

        var isNewUser = user is null;
        if (isNewUser)
        {
            user = User.Register(phone.Value, isPlatformAdmin: false, now);
            await users.AddAsync(user, ct);
        }

        var tokens = IssueTokens(user, now);
        await refreshTokens.AddAsync(tokens.RefreshTokenEntity, ct);

        if (isNewUser)
        {
            await audit.RecordAsync(
                AuditActions.UserRegistered,
                "user",
                user.Id.ToString(),
                actorUserId: user.Id,
                metadataJson: JsonSerializer.Serialize(new { phone = phone.Value }),
                ct: ct);
        }

        await audit.RecordAsync(
            AuditActions.LoginSucceeded,
            "user",
            user.Id.ToString(),
            actorUserId: user.Id,
            ct: ct);

        return tokens.Response;
    }

    public async Task<AuthTokensResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw AppException.BadRequest("auth.token_required", "Refresh token is required.");

        var now = timeProvider.GetUtcNow();
        var hash = IdentityHashing.Sha256Hex(request.RefreshToken.Trim());
        var token = await refreshTokens.FindByHashAsync(hash, ct);

        if (token is null)
            throw AppException.Unauthorized("auth.token_invalid", "Refresh token is not recognized.");

        if (!token.IsActive(now))
        {
            if (token.ReplacedByTokenId is not null)
            {
                // A rotated token was presented again → treat as a leaked token:
                // revoke the whole family, then require re-authentication.
                await refreshTokens.RevokeAllForUserAsync(token.UserId, now, ct);
                await unitOfWork.SaveChangesAsync(ct);
                await audit.RecordAsync(
                    AuditActions.TokenReuseDetected,
                    "user",
                    token.UserId.ToString(),
                    metadataJson: JsonSerializer.Serialize(new { presentedTokenId = token.Id }),
                    ct: ct);
                throw AppException.Unauthorized(
                    "auth.token_reused",
                    "This refresh token was already rotated. All sessions for this account were revoked.");
            }

            throw AppException.Unauthorized(
                token.IsExpired(now) ? "auth.token_expired" : "auth.token_revoked",
                "Refresh token is no longer valid.");
        }

        var user = await users.FindByIdAsync(token.UserId, ct);
        if (user is null)
            throw AppException.Unauthorized("auth.token_invalid", "Refresh token is not recognized.");
        RejectUnavailableAccount(user);

        var tokens = IssueTokens(user, now);
        await refreshTokens.AddAsync(tokens.RefreshTokenEntity, ct);
        token.Revoke(now, tokens.RefreshTokenEntity.Id);
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.TokenRefreshed,
            "user",
            user.Id.ToString(),
            actorUserId: user.Id,
            ct: ct);

        return tokens.Response;
    }

    public async Task LogoutAsync(Guid userId, LogoutRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw AppException.BadRequest("auth.token_required", "Refresh token is required.");

        var now = timeProvider.GetUtcNow();
        var hash = IdentityHashing.Sha256Hex(request.RefreshToken.Trim());
        var token = await refreshTokens.FindByHashAsync(hash, ct);

        if (token is not null && token.UserId != userId)
        {
            // Do not revoke someone else's token; record the attempt instead.
            await audit.RecordAsync(
                AuditActions.ForeignTokenRevokeAttempt,
                "refresh_token",
                token.Id.ToString(),
                actorUserId: userId,
                ct: ct);
            return;
        }

        if (token is not null && token.IsActive(now))
        {
            token.Revoke(now);
            await unitOfWork.SaveChangesAsync(ct);
            await audit.RecordAsync(
                AuditActions.TokenRevoked,
                "refresh_token",
                token.Id.ToString(),
                actorUserId: userId,
                ct: ct);
        }
    }

    public async Task<UserSummary> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId, ct)
            ?? throw AppException.Unauthorized("auth.user_not_found", "Account no longer exists.");
        RejectUnavailableAccount(user);
        return UserSummary.From(user);
    }

    // ---- internals ----

    private static PhoneNumber ParsePhone(string raw) =>
        PhoneNumber.TryParse(raw, out var phone)
            ? phone
            : throw AppException.BadRequest(
                "auth.phone_invalid",
                "Enter a valid phone number (e.g. +971501234567 or 0501234567).");

    private static void RejectUnavailableAccount(User? user)
    {
        if (user is null)
            return;
        if (user.Status == UserStatus.Blocked)
            throw AppException.Forbidden("auth.account_blocked", "This account is blocked.");
        if (user.Status == UserStatus.Deleted)
            throw AppException.Forbidden("auth.account_deleted", "This account was deleted.");
    }

    private (AuthTokensResponse Response, RefreshToken RefreshTokenEntity) IssueTokens(User user, DateTimeOffset now)
    {
        var access = tokenService.IssueAccessToken(user);
        var opaqueRefreshToken = tokenService.NewOpaqueToken();
        var refreshEntity = RefreshToken.Create(
            user.Id,
            IdentityHashing.Sha256Hex(opaqueRefreshToken),
            now,
            TimeSpan.FromDays(jwtOptions.RefreshTokenDays));

        var response = new AuthTokensResponse(
            "Bearer",
            access.Token,
            access.ExpiresInSeconds,
            opaqueRefreshToken,
            jwtOptions.RefreshTokenDays * 24 * 60 * 60,
            UserSummary.From(user));

        return (response, refreshEntity);
    }

    private async Task RecordLoginFailureAsync(
        string phoneNumber, string reason, CancellationToken ct, int? attemptsRemaining = null)
    {
        await audit.RecordAsync(
            AuditActions.LoginFailed,
            "phone",
            phoneNumber,
            metadataJson: JsonSerializer.Serialize(new { phone = phoneNumber, reason, attemptsRemaining }),
            ct: ct);
    }
}
