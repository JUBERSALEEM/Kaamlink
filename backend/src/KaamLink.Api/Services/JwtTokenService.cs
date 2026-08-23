using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KaamLink.Application.Abstractions;
using KaamLink.Application.Auth;
using KaamLink.Application.Common;
using KaamLink.Application.Options;
using KaamLink.Domain.Identity;
using Microsoft.IdentityModel.Tokens;

namespace KaamLink.Api.Services;

/// <summary>
/// Issues short-lived signed access tokens (HMAC-SHA256 — single-service monolith,
/// ADR 0002) and opaque refresh tokens (only hashes of those are ever persisted).
/// </summary>
public sealed class JwtTokenService(JwtOptions options, TimeProvider timeProvider) : ITokenService
{
    public AccessTokenIssue IssueAccessToken(User user)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(options.AccessTokenMinutes);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("phone", user.PhoneNumber),
            new("platform_admin", user.IsPlatformAdmin ? "true" : "false")
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new AccessTokenIssue(
            new JwtSecurityTokenHandler().WriteToken(token),
            (int)(expires - now).TotalSeconds);
    }

    public string NewOpaqueToken() => Base64Url.RandomToken();
}
