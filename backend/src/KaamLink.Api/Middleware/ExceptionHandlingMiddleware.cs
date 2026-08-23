using KaamLink.Application.Common;

namespace KaamLink.Api.Middleware;

/// <summary>
/// Maps AppException to problem-details (+ stable "code" for clients) and turns
/// unexpected exceptions into a generic 500 without leaking internals.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException exception)
        {
            await WriteProblemAsync(context, exception.StatusCode, exception.Code, exception.Message, exception.Details);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, 500, "server.error", "An unexpected error occurred.", null);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string code,
        string detail,
        IReadOnlyDictionary<string, object?>? details)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var payload = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.com/{status}",
            ["title"] = code,
            ["status"] = status,
            ["detail"] = detail,
            ["code"] = code
        };

        if (details is not null)
        {
            foreach (var (key, value) in details)
                payload[key] = value;
        }

        await context.Response.WriteAsJsonAsync(payload);
    }
}
