namespace KaamLink.Domain.Auditing;

/// <summary>
/// Immutable record of an important business/security event (Master Prompt §47).
/// Metadata is JSONB. NEVER store secrets here: no passwords, no OTP codes,
/// no token values (hashed or otherwise).
/// </summary>
public sealed class AuditLog
{
    public long Id { get; private set; }

    /// <summary>Null for system/anonymous events (e.g. OTP requested before login).</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>Dotted action name, e.g. "auth.login_succeeded". See <see cref="AuditActions"/>.</summary>
    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public string? MetadataJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private AuditLog()
    {
    }

    public static AuditLog Record(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId,
        string? metadataJson,
        DateTimeOffset now) => new()
    {
        ActorUserId = actorUserId,
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        MetadataJson = metadataJson,
        CreatedAt = now
    };
}
