using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Data;

public class StgProject : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ImportBatchId { get; set; } = Guid.NewGuid();
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string PmPersonCode { get; set; } = string.Empty;
    public string CurrentStageCode { get; set; } = string.Empty;
    public string? CurrentBuild { get; set; }
    public int? PlannedQty { get; set; }
    public string? CustomerRef { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StgProject()
    {
    }

    public StgProject(
        Guid id,
        string projectCode,
        string projectName,
        string pmPersonCode,
        string currentStageCode,
        Guid? tenantId = null,
        Guid importBatchId = default)
        : base(id)
    {
        ProjectCode = projectCode;
        ProjectName = projectName;
        PmPersonCode = pmPersonCode;
        CurrentStageCode = currentStageCode;
        TenantId = tenantId;
        ImportBatchId = importBatchId == default ? Guid.NewGuid() : importBatchId;
    }
}


