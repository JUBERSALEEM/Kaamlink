namespace KaamLink.Domain.Identity;

/// <summary>
/// Authentication identity: answers "who is this person?" — and nothing else.
/// Business data lives on WorkerProfile / CompanyMembership (Phase 03+), never here
/// (see docs/adr/0003-auth-vs-business-profiles.md).
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }

    /// <summary>
    /// Normalized E.164 phone number. Unique. Email OTP accounts receive a
    /// synthetic, non-dialable +999 identity so phone-OTP and email identities can
    /// coexist in the same user table without weakening phone verification.
    /// </summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>Confirmed email for OTP/password identities; null for phone-only accounts.</summary>
    public string? Email { get; private set; }

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

    /// <summary>
    /// First successful Supabase email OTP/password exchange registers the account. The
    /// synthetic phone is derived from the verified Supabase subject, not from a
    /// user-supplied number, so it cannot collide with a real phone identity.
    /// </summary>
    public static User RegisterFromEmail(string email, string syntheticPhoneNumber, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        PhoneNumber = syntheticPhoneNumber,
        Email = email,
        Status = UserStatus.Active,
        IsPlatformAdmin = false,
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
        Email = null;
        DisplayName = null;
        UpdatedAt = now;
    }

    public bool CanAuthenticate => Status == UserStatus.Active;
}
