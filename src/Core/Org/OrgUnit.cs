using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Org;

public class OrgUnit : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string? ParentCode { get; set; }
    public string? Team { get; set; }
    public string? ManagerPersonCode { get; set; }
    public Guid? SourceRecordId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public OrgUnit()
    {
    }

    public OrgUnit(Guid id, string unitCode, string unitName, string? parentCode = null, string? team = null, string? managerPersonCode = null, Guid? tenantId = null, Guid? sourceRecordId = null)
        : base(id)
    {
        UnitCode = unitCode;
        UnitName = unitName;
        ParentCode = parentCode;
        Team = team;
        ManagerPersonCode = managerPersonCode;
        TenantId = tenantId;
        SourceRecordId = sourceRecordId;
    }
}


