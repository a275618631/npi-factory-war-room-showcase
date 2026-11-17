using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Settings;

public class AppSetting : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AppSetting()
    {
    }

    public AppSetting(Guid id, string name, string value, Guid? tenantId = null)
        : base(id)
    {
        Name = name;
        Value = value;
        TenantId = tenantId;
        UpdatedAt = DateTime.UtcNow;
    }
}


