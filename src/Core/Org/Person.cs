using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Org;

public class Person : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string PersonCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Title { get; set; }
    public string? PhoneExt { get; set; }
    public string? SiteCode { get; set; }
    public string? RoleTags { get; set; }
    public Guid? SourceRecordId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Person()
    {
    }

    public Person(Guid id, string personCode, string displayName, string unitCode, Guid? tenantId = null, Guid? sourceRecordId = null)
        : base(id)
    {
        PersonCode = personCode;
        DisplayName = displayName;
        UnitCode = unitCode;
        TenantId = tenantId;
        SourceRecordId = sourceRecordId;
    }
}


