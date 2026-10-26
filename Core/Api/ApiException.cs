namespace KaamLink.Core.Api
{
    /// <summary>
    /// Failure from an API call. <see cref="Code"/> carries the backend's stable
    /// machine-readable problem code (e.g. auth.otp_invalid, rate.limit_exceeded);
    /// a null <see cref="Status"/> means the call never reached the server.
    /// View models map codes to localized messages — raw Detail is never shown to users.
    /// </summary>
    public sealed class ApiException : Exception
    {
        public const string NoInternetCode = "client.no_internet";
        public const string ApiNotConfiguredCode = "client.api_not_configured";

        public ApiException(string message, int? status, string? code, Exception? innerException = null)
            : base(message, innerException)
        {
            Status = status;
            Code = code;
        }

        /// <summary>HTTP status when the server answered; null when offline or misconfigured.</summary>
        public int? Status { get; }

        /// <summary>Stable backend/client error code, e.g. "worker.profile_not_found".</summary>
        public string? Code { get; }

        public bool IsOffline => Status is null;

        public bool IsNoInternet => Code == NoInternetCode;

        public bool IsMisconfigured => Code == ApiNotConfiguredCode;

        public static ApiException Offline(Exception inner) =>
            new("The request could not reach the server.", status: null, code: null, inner);

        public static ApiException NoInternet() =>
            new("The device has no usable network connection.", status: null, code: NoInternetCode);

        public static ApiException Misconfigured(string issue) =>
            new(issue, status: null, code: ApiNotConfiguredCode);
    }
}
