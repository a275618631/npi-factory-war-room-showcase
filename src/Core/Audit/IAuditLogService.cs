namespace Core.Audit;

public record AuditLogDto(
    Guid Id,
    Guid? TenantId,
    string Category,
    string Action,
    string Target,
    string? OldValue,
    string? NewValue,
    string OperatorId,
    string OperatorName,
    DateTime Timestamp,
    string? Details);

public interface IAuditLogService
{
    Task RecordAsync(
        string category,
        string action,
        string target,
        string? oldValue,
        string? newValue,
        string? operatorId = null,
        string? operatorName = null,
        string? details = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDto>> QueryAsync(
        string? category = null,
        string? target = null,
        int take = 50,
        CancellationToken cancellationToken = default);
}


