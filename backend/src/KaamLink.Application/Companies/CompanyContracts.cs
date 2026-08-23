namespace KaamLink.Application.Companies;

// ---- Responses ----

public sealed record CompanySummaryResponse(
    Guid Id,
    string Name,
    string Category,
    string Emirate,
    string? Area,
    string? TradeLicenseNumber,
    string VerificationState,
    string? VerificationReason,
    DateTimeOffset? VerifiedAt,
    string Role,
    DateTimeOffset CreatedAt);

// ---- Requests ----

public sealed record CreateCompanyRequest
{
    public string Name { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Emirate { get; init; } = string.Empty;

    public string? Area { get; init; }

    public string? TradeLicenseNumber { get; init; }

    public string? Description { get; init; }
}

public sealed record ReviewCompanyRequest
{
    public bool Approve { get; init; }

    /// <summary>Required when rejecting; shown to the company.</summary>
    public string? Reason { get; init; }
}
