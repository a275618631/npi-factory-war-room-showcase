using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Projects;

public class Project : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string PmPersonCode { get; set; } = string.Empty;
    public string CurrentStageCode { get; set; } = string.Empty;
    public string? CurrentBuild { get; set; }
    public int? PlannedQty { get; set; }
    public string? CustomerRef { get; set; }
    public Guid? SourceRecordId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Project() { }

    public Project(
        Guid id,
        string projectCode,
        string projectName,
        string pmPersonCode,
        string currentStageCode,
        Guid? tenantId = null,
        string? currentBuild = null,
        int? plannedQty = null,
        string? customerRef = null,
        Guid? sourceRecordId = null)
        : base(id)
    {
        ProjectCode = projectCode;
        ProjectName = projectName;
        PmPersonCode = pmPersonCode;
        CurrentStageCode = currentStageCode;
        TenantId = tenantId;
        CurrentBuild = currentBuild;
        PlannedQty = plannedQty;
        CustomerRef = customerRef;
        SourceRecordId = sourceRecordId;
        UpdatedAt = DateTime.UtcNow;
    }
}


