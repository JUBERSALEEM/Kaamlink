namespace KaamLink.Application.Common;

/// <summary>
/// Business/application failure with a stable machine-readable <see cref="Code"/>
/// (mapped to a problem-details response by the Api middleware). Codes follow
/// "area.snake_case" and are part of the API contract — never rename casually.
/// </summary>
public sealed class AppException : Exception
{
    public string Code { get; }

    public int StatusCode { get; }

    public IReadOnlyDictionary<string, object?>? Details { get; }

    private AppException(
        string code,
        int statusCode,
        string message,
        IReadOnlyDictionary<string, object?>? details = null) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }

    public static AppException BadRequest(
        string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        => new(code, StatusCodes.BadRequest, message, details);

    public static AppException Unauthorized(
        string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        => new(code, StatusCodes.Unauthorized, message, details);

    public static AppException Forbidden(
        string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        => new(code, StatusCodes.Forbidden, message, details);

    public static AppException NotFound(
        string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        => new(code, StatusCodes.NotFound, message, details);

    public static AppException TooManyRequests(
        string code, string message, int retryAfterSeconds)
        => new(code, StatusCodes.TooManyRequests, message,
            new Dictionary<string, object?> { ["retryAfterSeconds"] = retryAfterSeconds });

    /// <summary>HTTP status constants without pulling ASP.NET into the Application layer.</summary>
    private static class StatusCodes
    {
        public const int BadRequest = 400;
        public const int Unauthorized = 401;
        public const int Forbidden = 403;
        public const int NotFound = 404;
        public const int TooManyRequests = 429;
    }
}
