using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using KaamLink.Application.Abstractions;
using KaamLink.Application.Common;
using KaamLink.Application.Options;

namespace KaamLink.Infrastructure.Identity;

/// <summary>
/// Verifies a Supabase email OTP/password session token by calling GoTrue's /user endpoint.
/// The token is sent to the issuing Supabase project; only a confirmed email and
/// stable subject are returned to the KaamLink backend, which then issues its own
/// short-lived JWT and rotating refresh token.
/// </summary>
public sealed class SupabaseUserVerifier(HttpClient http, SupabaseOptions options) : ISupabaseUserVerifier
{
    public async Task<SupabaseIdentity> VerifyAsync(string accessToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw AppException.BadRequest("auth.supabase_token_required", "A sign-in token is required.");

        if (!options.IsConfigured)
            throw AppException.Unauthorized(
                "auth.supabase_not_configured",
                "Email sign-in is not configured on the server.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "auth/v1/user");
        request.Headers.TryAddWithoutValidation("apikey", options.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw AppException.Unauthorized(
                "auth.supabase_unavailable",
                "We could not verify this sign-in link. Please try again.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw AppException.Unauthorized(
                "auth.supabase_unavailable",
                "We could not verify this sign-in link. Please try again.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw AppException.TooManyRequests(
                    "auth.supabase_rate_limited",
                    "Too many sign-in attempts. Please wait a minute and try again.",
                    60);

            if (!response.IsSuccessStatusCode)
                throw AppException.Unauthorized(
                    "auth.supabase_invalid",
                    "This sign-in link is invalid or expired. Request a new one.");

            try
            {
                using var document = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var root = document.RootElement;
                var subject = GetString(root, "id");
                var email = GetString(root, "email");
                var confirmedAt = GetString(root, "email_confirmed_at");
                var passwordSet = ReadPasswordSetFlag(root);

                if (string.IsNullOrWhiteSpace(subject)
                    || string.IsNullOrWhiteSpace(email)
                    || string.IsNullOrWhiteSpace(confirmedAt)
                    || !email.Contains('@'))
                {
                    throw AppException.Unauthorized(
                        "auth.supabase_invalid",
                        "This sign-in link is invalid or expired. Request a new one.");
                }

                return new SupabaseIdentity(subject, email.Trim().ToLowerInvariant(), passwordSet);
            }
            catch (JsonException)
            {
                throw AppException.Unauthorized(
                    "auth.supabase_invalid",
                    "This sign-in link is invalid or expired. Request a new one.");
            }
        }
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// The app sets user_metadata.password_set when it calls PUT /auth/v1/user.
    /// Missing/false means first-time OTP users still need password creation.
    /// </summary>
    private static bool ReadPasswordSetFlag(JsonElement root)
    {
        if (!root.TryGetProperty("user_metadata", out var metadata)
            || metadata.ValueKind != JsonValueKind.Object
            || !metadata.TryGetProperty("password_set", out var flag))
            return false;

        return flag.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => string.Equals(flag.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
