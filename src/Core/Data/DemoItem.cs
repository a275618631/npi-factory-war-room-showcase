using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Data;

public class DemoItem : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    protected DemoItem()
    {
    }

    public DemoItem(Guid id, string name, Guid? tenantId = null, string? description = null)
        : base(id)
    {
        Name = name;
        TenantId = tenantId;
        Description = description;
    }
}


