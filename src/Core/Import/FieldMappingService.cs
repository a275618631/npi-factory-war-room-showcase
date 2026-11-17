using System.Text.Json;
using Core.Settings;
using Volo.Abp.DependencyInjection;

namespace Core.Import;

public class FieldMappingService : IFieldMappingService, ITransientDependency
{
    private readonly ISettingCenterService _settingCenterService;

    public FieldMappingService(ISettingCenterService settingCenterService)
    {
        _settingCenterService = settingCenterService;
    }

    public async Task<Dictionary<string, string>> GetMappingAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var key = SettingDefinitions.Keys.ImportFieldMappingPrefix + tableName.Trim().ToLowerInvariant();
        var jsonValue = await _settingCenterService.GetValueAsync(key, cancellationToken);

        if (string.IsNullOrWhiteSpace(jsonValue) || jsonValue == "{}")
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonValue);
            return dict != null
                ? new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task SetMappingAsync(string tableName, Dictionary<string, string> mappings, CancellationToken cancellationToken = default)
    {
        var key = SettingDefinitions.Keys.ImportFieldMappingPrefix + tableName.Trim().ToLowerInvariant();
        var jsonValue = JsonSerializer.Serialize(mappings);
        await _settingCenterService.SetValueAsync(key, jsonValue, cancellationToken);
    }
}


