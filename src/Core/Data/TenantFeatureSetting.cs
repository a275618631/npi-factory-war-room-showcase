using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Data;

public class TenantFeatureSetting : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    protected TenantFeatureSetting()
    {
    }

    public TenantFeatureSetting(Guid id, string name, string value, Guid? tenantId = null)
        : base(id)
    {
        Name = name;
        Value = value;
        TenantId = tenantId;
    }
}


