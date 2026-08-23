namespace KaamLink.Domain.Identity;

/// <summary>
/// Authentication identity: answers "who is this person?" — and nothing else.
/// Business data lives on WorkerProfile / CompanyMembership (Phase 03+), never here
/// (see docs/adr/0003-auth-vs-business-profiles.md).
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }

    /// <summary>Normalized E.164 phone number. Unique — the primary login identifier.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>Optional; set during profile onboarding (Phase 03), not at registration.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>"en" or "ar" today; drives notification/app copy language.</summary>
    public string? PreferredLocale { get; private set; }

    public UserStatus Status { get; private set; }

    /// <summary>Platform operators only. Company-side roles live on CompanyMember, not here.</summary>
    public bool IsPlatformAdmin { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private User()
    {
    }

    /// <summary>First successful OTP verification registers the account.</summary>
    public static User Register(string phoneNumber, bool isPlatformAdmin, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        PhoneNumber = phoneNumber,
        Status = UserStatus.Active,
        IsPlatformAdmin = isPlatformAdmin,
        CreatedAt = now,
        UpdatedAt = now
    };

    public void Block(DateTimeOffset now)
    {
        Status = UserStatus.Blocked;
        UpdatedAt = now;
    }

    /// <summary>Account deletion anonymizes identity data; audit trail is preserved.</summary>
    public void Anonymize(DateTimeOffset now)
    {
        Status = UserStatus.Deleted;
        PhoneNumber = $"deleted:{Id}";
        DisplayName = null;
        UpdatedAt = now;
    }

    public bool CanAuthenticate => Status == UserStatus.Active;
}
