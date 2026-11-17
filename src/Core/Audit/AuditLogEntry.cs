using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Audit;

public class AuditLogEntry : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string OperatorId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ClientIp { get; set; }
    public string? Details { get; set; }

    public AuditLogEntry() { }

    public AuditLogEntry(
        Guid id,
        string category,
        string action,
        string target,
        string? oldValue,
        string? newValue,
        string operatorId,
        string operatorName,
        Guid? tenantId = null,
        string? details = null)
        : base(id)
    {
        Category = category;
        Action = action;
        Target = target;
        OldValue = oldValue;
        NewValue = newValue;
        OperatorId = operatorId;
        OperatorName = operatorName;
        TenantId = tenantId;
        Details = details;
        Timestamp = DateTime.UtcNow;
    }
}


