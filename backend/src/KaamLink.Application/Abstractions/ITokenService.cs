using KaamLink.Domain.Identity;

namespace KaamLink.Application.Abstractions;

public interface ITokenService
{
    AccessTokenIssue IssueAccessToken(User user);

    /// <summary>Cryptographically random opaque token string (base64url). Only its hash may be persisted.</summary>
    string NewOpaqueToken();
}
