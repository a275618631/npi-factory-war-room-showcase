using Volo.Abp.Domain.Entities;

namespace Core.Data;

public class Tenant : Entity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    protected Tenant()
    {
    }

    public Tenant(Guid id, string name, string code)
        : base(id)
    {
        Name = name;
        Code = code;
    }
}


