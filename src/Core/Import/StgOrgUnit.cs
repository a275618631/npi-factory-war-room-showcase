using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Import;

public class StgOrgUnit : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ImportBatchId { get; set; } = Guid.NewGuid();
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string? ParentCode { get; set; }
    public string? Team { get; set; }
    public string? ManagerPersonCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StgOrgUnit()
    {
    }

    public StgOrgUnit(Guid id, string unitCode, string unitName, string? parentCode = null, string? team = null, string? managerPersonCode = null, Guid? tenantId = null, Guid importBatchId = default)
        : base(id)
    {
        UnitCode = unitCode;
        UnitName = unitName;
        ParentCode = parentCode;
        Team = team;
        ManagerPersonCode = managerPersonCode;
        TenantId = tenantId;
        ImportBatchId = importBatchId == default ? Guid.NewGuid() : importBatchId;
    }
}


