using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Projects;

public class StgStagePlan : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ImportBatchId { get; set; } = Guid.NewGuid();
    public string ProjectCode { get; set; } = string.Empty;
    public string StageCode { get; set; } = string.Empty;
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public DateOnly? GateDate { get; set; }
    public string? GateOwnerPersonCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StgStagePlan() { }

    public StgStagePlan(
        Guid id,
        string projectCode,
        string stageCode,
        DateOnly plannedStart,
        DateOnly plannedEnd,
        DateOnly? actualStart = null,
        DateOnly? actualEnd = null,
        DateOnly? gateDate = null,
        string? gateOwnerPersonCode = null,
        Guid? tenantId = null,
        Guid importBatchId = default)
        : base(id)
    {
        ProjectCode = projectCode;
        StageCode = stageCode;
        PlannedStart = plannedStart;
        PlannedEnd = plannedEnd;
        ActualStart = actualStart;
        ActualEnd = actualEnd;
        GateDate = gateDate;
        GateOwnerPersonCode = gateOwnerPersonCode;
        TenantId = tenantId;
        ImportBatchId = importBatchId == default ? Guid.NewGuid() : importBatchId;
    }
}


