using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Security;

public class TenantApiKey : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TenantApiKey()
    {
    }

    public TenantApiKey(Guid id, string key, string name, Guid? tenantId = null, bool isActive = true)
        : base(id)
    {
        Key = key;
        Name = name;
        TenantId = tenantId;
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
    }
}


