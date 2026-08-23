namespace KaamLink.Core.Api
{
    /// <summary>
    /// Failure from an API call. <see cref="Code"/> carries the backend's stable
    /// machine-readable problem code (e.g. auth.otp_invalid, rate.limit_exceeded);
    /// a null <see cref="Status"/> means the call never reached the server (offline).
    /// View models map codes to localized messages — raw Detail is never shown to users.
    /// </summary>
    public sealed class ApiException : Exception
    {
        public ApiException(string message, int? status, string? code, Exception? innerException = null)
            : base(message, innerException)
        {
            Status = status;
            Code = code;
        }

        /// <summary>HTTP status when the server answered; null when offline.</summary>
        public int? Status { get; }

        /// <summary>Stable backend error code, e.g. "worker.profile_not_found".</summary>
        public string? Code { get; }

        public bool IsOffline => Status is null;

        public static ApiException Offline(Exception inner) =>
            new("The request could not reach the server.", status: null, code: null, inner);
    }
}
