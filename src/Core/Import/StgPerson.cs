using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Import;

public class StgPerson : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ImportBatchId { get; set; } = Guid.NewGuid();
    public string PersonCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Title { get; set; }
    public string? PhoneExt { get; set; }
    public string? SiteCode { get; set; }
    public string? RoleTags { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StgPerson()
    {
    }

    public StgPerson(Guid id, string personCode, string displayName, string unitCode, Guid? tenantId = null, Guid importBatchId = default)
        : base(id)
    {
        PersonCode = personCode;
        DisplayName = displayName;
        UnitCode = unitCode;
        TenantId = tenantId;
        ImportBatchId = importBatchId == default ? Guid.NewGuid() : importBatchId;
    }
}


