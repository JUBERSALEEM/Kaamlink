namespace KaamLink.Application.Auth;

public interface IAuthService
{
    Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken ct = default);

    Task<AuthTokensResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default);

    Task<AuthTokensResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);

    Task LogoutAsync(Guid userId, LogoutRequest request, CancellationToken ct = default);

    Task<UserSummary> GetMeAsync(Guid userId, CancellationToken ct = default);
}
