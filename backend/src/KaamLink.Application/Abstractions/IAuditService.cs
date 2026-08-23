namespace KaamLink.Application.Abstractions;

/// <summary>
/// Write-through audit trail (Master Prompt §47). Implementations persist
/// immediately so security events (login failures, token reuse) survive even
/// when the surrounding business operation then fails.
/// NEVER pass secrets in metadata (no codes, tokens, passwords).
/// </summary>
public interface IAuditService
{
    Task RecordAsync(
        string action,
        string entityType,
        string? entityId = null,
        Guid? actorUserId = null,
        string? metadataJson = null,
        CancellationToken ct = default);
}
