using Core.Audit;
using Core.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Core.Settings;

public class SettingCenterService : ISettingCenterService, ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAuditLogService? _auditLogService;

    public SettingCenterService(
        NpiDbContext dbContext,
        ICurrentTenant currentTenant,
        IAuditLogService? auditLogService = null)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _auditLogService = auditLogService;
    }

    public async Task<string> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        // 先查詢當前租戶是否有覆寫值
        var setting = await _dbContext.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == key, cancellationToken);

        if (setting != null)
        {
            return setting.Value;
        }

        // 若無覆寫，回傳平台預設值
        var def = SettingDefinitions.Find(key);
        return def?.DefaultValue ?? string.Empty;
    }

    public async Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var stringValue = await GetValueAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(stringValue))
        {
            return default;
        }

        try
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter.CanConvertFrom(typeof(string)))
            {
                return (T?)converter.ConvertFromString(stringValue);
            }

            return JsonSerializer.Deserialize<T>(stringValue);
        }
        catch
        {
            return default;
        }
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.Id;
        var oldValue = await GetValueAsync(key, cancellationToken);

        var existing = await _dbContext.AppSettings
            .FirstOrDefaultAsync(s => s.Name == key, cancellationToken);

        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var newSetting = new AppSetting(Guid.NewGuid(), key, value, tenantId);
            await _dbContext.AppSettings.AddAsync(newSetting, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditLogService != null && oldValue != value)
        {
            var def = SettingDefinitions.Find(key);
            var category = def?.Category ?? SettingCategories.CompanyBasic;
            await _auditLogService.RecordAsync(
                category: category,
                action: "UpdateSetting",
                target: key,
                oldValue: oldValue,
                newValue: value,
                details: $"設定「{def?.DisplayName ?? key}」變更",
                cancellationToken: cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SettingItemDto>> GetAllSettingsAsync(CancellationToken cancellationToken = default)
    {
        var overriddenSettings = await _dbContext.AppSettings
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Name, s => s.Value, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var list = new List<SettingItemDto>();
        foreach (var def in SettingDefinitions.All)
        {
            var isOverridden = overriddenSettings.TryGetValue(def.Key, out var overriddenValue);
            var effectiveValue = isOverridden ? overriddenValue! : def.DefaultValue;

            list.Add(new SettingItemDto(
                def.Key,
                def.DisplayName,
                def.Category,
                def.ValueType,
                effectiveValue,
                def.DefaultValue,
                isOverridden,
                def.Description));
        }

        return list;
    }

    public async Task<string> ExportSettingsJsonAsync(CancellationToken cancellationToken = default)
    {
        var all = await GetAllSettingsAsync(cancellationToken);
        var exportDict = all.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(exportDict, options);
    }

    public async Task ImportSettingsJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (dict == null || dict.Count == 0)
        {
            return;
        }

        var tenantId = _currentTenant.Id;
        var existingSettings = await _dbContext.AppSettings
            .ToDictionaryAsync(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var (k, v) in dict)
        {
            var oldValue = await GetValueAsync(k, cancellationToken);
            if (existingSettings.TryGetValue(k, out var existing))
            {
                existing.Value = v;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var newSetting = new AppSetting(Guid.NewGuid(), k, v, tenantId);
                await _dbContext.AppSettings.AddAsync(newSetting, cancellationToken);
                existingSettings[k] = newSetting;
            }

            if (_auditLogService != null && oldValue != v)
            {
                var def = SettingDefinitions.Find(k);
                var category = def?.Category ?? SettingCategories.CompanyBasic;
                await _auditLogService.RecordAsync(
                    category: category,
                    action: "ImportSetting",
                    target: k,
                    oldValue: oldValue,
                    newValue: v,
                    details: $"透過匯入變更設定「{def?.DisplayName ?? k}」",
                    cancellationToken: cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}


