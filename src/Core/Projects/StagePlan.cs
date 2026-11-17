using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Projects;

public class StagePlan : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string StageCode { get; set; } = string.Empty;
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public DateOnly? GateDate { get; set; }
    public string? GateOwnerPersonCode { get; set; }
    public int DelayDays { get; set; }
    public Guid? SourceRecordId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public StagePlan() { }

    public StagePlan(
        Guid id,
        string projectCode,
        string stageCode,
        DateOnly plannedStart,
        DateOnly plannedEnd,
        DateOnly? actualStart = null,
        DateOnly? actualEnd = null,
        DateOnly? gateDate = null,
        string? gateOwnerPersonCode = null,
        int delayDays = 0,
        Guid? tenantId = null,
        Guid? sourceRecordId = null)
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
        DelayDays = delayDays;
        TenantId = tenantId;
        SourceRecordId = sourceRecordId;
        UpdatedAt = DateTime.UtcNow;
    }
}


