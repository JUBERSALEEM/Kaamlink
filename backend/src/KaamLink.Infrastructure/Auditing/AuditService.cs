using KaamLink.Application.Abstractions;
using KaamLink.Domain.Auditing;
using KaamLink.Infrastructure.Persistence;

namespace KaamLink.Infrastructure.Auditing;

/// <summary>
/// Write-through audit persistence (see IAuditService). Flushes the shared scoped
/// context immediately so security events are never lost when the surrounding
/// business operation subsequently throws.
/// </summary>
public sealed class AuditService(AppDbContext db, TimeProvider timeProvider) : IAuditService
{
    public async Task RecordAsync(
        string action,
        string entityType,
        string? entityId = null,
        Guid? actorUserId = null,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        var entry = AuditLog.Record(
            actorUserId,
            action,
            entityType,
            entityId,
            metadataJson,
            timeProvider.GetUtcNow());

        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
