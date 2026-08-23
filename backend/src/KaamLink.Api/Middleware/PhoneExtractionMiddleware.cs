using System.Text;
using System.Text.Json;
using KaamLink.Domain.Shared;

namespace KaamLink.Api.Middleware;

/// <summary>
/// Reads the (normalized) phone number from small OTP request bodies and stores it
/// in HttpContext.Items so the rate limiter can partition per target phone —
/// the unit an attacker abuses — instead of only per source IP. Falls back to the
/// IP partition for missing/malformed bodies. The body stream is rewound so model
/// binding is unaffected.
/// </summary>
public sealed class PhoneExtractionMiddleware(RequestDelegate next)
{
    public const string PhoneItemKey = "KaamLink.RateLimiting.Phone";

    private const long MaxBodyBytes = 1024;

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        if (HttpMethods.IsPost(request.Method)
            && request.Path.StartsWithSegments("/api/v1/auth/otp", out _)
            && request.ContentLength is > 0 and <= MaxBodyBytes
            && request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            request.EnableBuffering();

            using var reader = new StreamReader(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync(context.RequestAborted);
            request.Body.Position = 0;

            TryStorePhone(context, body);
        }

        await next(context);
    }

    private static void TryStorePhone(HttpContext context, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("phone", out var phoneElement)
                && phoneElement.ValueKind == JsonValueKind.String
                && PhoneNumber.TryParse(phoneElement.GetString(), out var phone))
            {
                context.Items[PhoneItemKey] = phone.Value;
            }
        }
        catch (JsonException)
        {
            // Malformed JSON: model binding will produce the 400; rate limiting falls back to IP.
        }
    }
}
