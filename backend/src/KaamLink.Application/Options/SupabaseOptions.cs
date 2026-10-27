namespace KaamLink.Application.Options;

/// <summary>
/// Public Supabase project settings used to verify email OTP/password session tokens.
/// The publishable/anon key is safe to ship to clients; the service-role key is
/// never used here and must never be committed to the mobile app.
/// </summary>
public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>Project URL, e.g. https://your-project-ref.supabase.co.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Publishable/anon API key. Sent as `apikey` on GoTrue requests.</summary>
    public string AnonKey { get; init; } = string.Empty;

    public bool IsConfigured =>
        Uri.TryCreate(Url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrWhiteSpace(AnonKey);
}
