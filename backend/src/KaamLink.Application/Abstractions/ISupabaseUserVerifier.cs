namespace KaamLink.Application.Abstractions;

/// <summary>A verified Supabase Auth identity returned by GET /auth/v1/user.</summary>
/// <param name="Subject">Stable Supabase user id (the JWT `sub` claim).</param>
/// <param name="Email">Confirmed, normalized email address.</param>
public sealed record SupabaseIdentity(string Subject, string Email, bool PasswordSet);

/// <summary>
/// Server-side port for validating a Supabase access token. It calls GoTrue with
/// the token rather than trusting a client-supplied email or decoding a JWT with
/// an unvalidated signature.
/// </summary>
public interface ISupabaseUserVerifier
{
    Task<SupabaseIdentity> VerifyAsync(string accessToken, CancellationToken ct = default);
}
