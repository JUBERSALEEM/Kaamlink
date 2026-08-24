using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace KaamLink.Api.Middleware;

/// <summary>Writes a problem-details body for rate-limit rejections (429), matching the API error contract.</summary>
public static class RateLimitRejection
{
    public static Task WriteProblemDetailsAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.ContentType = "application/problem+json";

        return http.Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.com/429",
            title = "rate.limit_exceeded",
            status = 429,
            detail = "Too many requests. Please slow down and try again shortly.",
            code = "rate.limit_exceeded"
        }, cancellationToken: cancellationToken);
    }
}
