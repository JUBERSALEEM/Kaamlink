namespace KaamLink.Domain.Identity;

/// <summary>
/// Lifecycle state of a platform account. Stored as text in the database
/// (see UserConfiguration) so stored values survive enum reordering.
/// </summary>
public enum UserStatus
{
    Active = 0,
    Blocked = 1,
    Deleted = 2
}
