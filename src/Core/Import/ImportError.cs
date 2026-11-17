using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Import;

public class ImportError : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ImportBatchId { get; set; } = Guid.NewGuid();
    public int RowNumber { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OriginalValue { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ImportError()
    {
    }

    public ImportError(
        Guid id,
        Guid importBatchId,
        int rowNumber,
        string fieldName,
        string? originalValue,
        string reason,
        string? suggestion = null,
        Guid? tenantId = null)
        : base(id)
    {
        ImportBatchId = importBatchId;
        RowNumber = rowNumber;
        FieldName = fieldName;
        OriginalValue = originalValue;
        Reason = reason;
        Suggestion = suggestion;
        TenantId = tenantId;
    }
}


