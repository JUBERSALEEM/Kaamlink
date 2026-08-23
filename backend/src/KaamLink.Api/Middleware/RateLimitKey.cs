namespace KaamLink.Api.Middleware;

/// <summary>
/// Rate-limit partition key: the normalized target phone when known
/// (see PhoneExtractionMiddleware), otherwise the caller IP.
/// </summary>
public static class RateLimitKey
{
    public static string For(HttpContext context)
    {
        if (context.Items.TryGetValue(PhoneExtractionMiddleware.PhoneItemKey, out var value)
            && value is string phone
            && phone.Length > 0)
        {
            return $"phone:{phone}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
