using Core.Data;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Core.UserPreference;

public record UserUiPreferenceDto(
    string? ViewMode,
    string? Motion,
    string? LastProjectCode,
    DateTime UpdatedAt);

public record UpdateUserUiPreferenceInput(
    string? ViewMode,
    string? Motion,
    string? LastProjectCode);

public interface IUserPreferenceService
{
    Task<UserUiPreferenceDto> GetAsync(string userId);
    Task<UserUiPreferenceDto> SetAsync(string userId, UpdateUserUiPreferenceInput input);
}

public class UserPreferenceService : IUserPreferenceService, ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public UserPreferenceService(
        NpiDbContext dbContext,
        ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    public async Task<UserUiPreferenceDto> GetAsync(string userId)
    {
        var tenantId = _currentTenant.Id;
        var pref = await _dbContext.UserUiPreferences
            .Where(p => p.TenantId == tenantId && p.UserId == userId)
            .FirstOrDefaultAsync();

        if (pref == null)
        {
            return new UserUiPreferenceDto(null, null, null, DateTime.UtcNow);
        }

        return new UserUiPreferenceDto(
            pref.ViewMode,
            pref.Motion,
            pref.LastProjectCode,
            pref.UpdatedAt);
    }

    public async Task<UserUiPreferenceDto> SetAsync(string userId, UpdateUserUiPreferenceInput input)
    {
        var tenantId = _currentTenant.Id;
        var pref = await _dbContext.UserUiPreferences
            .Where(p => p.TenantId == tenantId && p.UserId == userId)
            .FirstOrDefaultAsync();

        if (pref == null)
        {
            pref = new UserUiPreference(
                Guid.NewGuid(),
                userId,
                tenantId,
                input.ViewMode,
                input.Motion,
                input.LastProjectCode);
            await _dbContext.UserUiPreferences.AddAsync(pref);
        }
        else
        {
            if (input.ViewMode != null)
            {
                pref.ViewMode = input.ViewMode;
            }
            if (input.Motion != null)
            {
                pref.Motion = input.Motion;
            }
            if (input.LastProjectCode != null)
            {
                pref.LastProjectCode = input.LastProjectCode;
            }
            pref.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        return new UserUiPreferenceDto(
            pref.ViewMode,
            pref.Motion,
            pref.LastProjectCode,
            pref.UpdatedAt);
    }
}


