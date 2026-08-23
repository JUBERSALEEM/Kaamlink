using KaamLink.Application.Abstractions;
using KaamLink.Application.Common;

namespace KaamLink.Api.Services;

/// <summary>Resolves the authenticated caller from the JWT "sub" claim.</summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(value, out var userId))
            {
                throw AppException.Unauthorized(
                    "auth.token_subject_invalid",
                    "Authenticated identity is missing a valid subject.");
            }

            return userId;
        }
    }
}
