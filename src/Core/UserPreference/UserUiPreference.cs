using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.UserPreference;

public class UserUiPreference : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? ViewMode { get; set; } // "relaxed", "overview"
    public string? Motion { get; set; } // "on", "off", "unset"
    public string? LastProjectCode { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public UserUiPreference() { }

    public UserUiPreference(
        Guid id,
        string userId,
        Guid? tenantId = null,
        string? viewMode = null,
        string? motion = null,
        string? lastProjectCode = null)
        : base(id)
    {
        UserId = userId;
        TenantId = tenantId;
        ViewMode = viewMode;
        Motion = motion;
        LastProjectCode = lastProjectCode;
        UpdatedAt = DateTime.UtcNow;
    }
}


