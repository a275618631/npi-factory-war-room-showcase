using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace Core.Settings;

public class StageConfigItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Color { get; set; } = "#409EFF";
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

public interface IStageSettingsService
{
    Task<List<StageConfigItem>> GetStagesAsync(CancellationToken cancellationToken = default);
    Task<List<StageConfigItem>> SaveStagesAsync(List<StageConfigItem> stages, CancellationToken cancellationToken = default);
    Task<bool> IsStageActiveAsync(string stageCode, CancellationToken cancellationToken = default);
    Task<List<string>> GetActiveStageCodesAsync(CancellationToken cancellationToken = default);
}

public class StageSettingsService : IStageSettingsService, ITransientDependency
{
    private readonly ISettingCenterService _settingCenterService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public StageSettingsService(ISettingCenterService settingCenterService)
    {
        _settingCenterService = settingCenterService;
    }

    public async Task<List<StageConfigItem>> GetStagesAsync(CancellationToken cancellationToken = default)
    {
        var json = await _settingCenterService.GetValueAsync(SettingDefinitions.Keys.StageDefinitions, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return GetDefaultStages();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<StageConfigItem>>(json, JsonOptions);
            if (items == null || items.Count == 0)
            {
                return GetDefaultStages();
            }

            return items.OrderBy(s => s.Order).ToList();
        }
        catch
        {
            return GetDefaultStages();
        }
    }

    public async Task<List<StageConfigItem>> SaveStagesAsync(List<StageConfigItem> stages, CancellationToken cancellationToken = default)
    {
        if (stages == null || stages.Count == 0)
        {
            throw new ArgumentException("Stages list cannot be empty.", nameof(stages));
        }

        // 重新確認 Order 序列
        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i].Order <= 0)
            {
                stages[i].Order = i + 1;
            }
        }

        var ordered = stages.OrderBy(s => s.Order).ToList();
        var json = JsonSerializer.Serialize(ordered, JsonOptions);

        // 1. 儲存完整階段設定
        await _settingCenterService.SetValueAsync(SettingDefinitions.Keys.StageDefinitions, json, cancellationToken);

        // 2. 同步更新 Stage.List（僅包含有效/啟用的階段代碼），使匯入轉換服務自動取得最新啟用名單
        var activeCodes = ordered.Where(s => s.IsActive).Select(s => s.Code.Trim()).ToList();
        var stageListStr = string.Join(",", activeCodes);
        await _settingCenterService.SetValueAsync(SettingDefinitions.Keys.StageList, stageListStr, cancellationToken);

        return ordered;
    }

    public async Task<bool> IsStageActiveAsync(string stageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stageCode))
        {
            return false;
        }

        var stages = await GetStagesAsync(cancellationToken);
        var match = stages.FirstOrDefault(s => string.Equals(s.Code, stageCode.Trim(), StringComparison.OrdinalIgnoreCase));
        return match != null && match.IsActive;
    }

    public async Task<List<string>> GetActiveStageCodesAsync(CancellationToken cancellationToken = default)
    {
        var stages = await GetStagesAsync(cancellationToken);
        return stages.Where(s => s.IsActive).OrderBy(s => s.Order).Select(s => s.Code).ToList();
    }

    private static List<StageConfigItem> GetDefaultStages()
    {
        return new List<StageConfigItem>
        {
            new() { Code = "Concept", Name = "概念", Order = 1, Color = "#909399", IsActive = true },
            new() { Code = "Feasibility", Name = "可行性", Order = 2, Color = "#E6A23C", IsActive = true },
            new() { Code = "EVT", Name = "工程驗證", Order = 3, Color = "#409EFF", IsActive = true },
            new() { Code = "DVT", Name = "設計驗證", Order = 4, Color = "#67C23A", IsActive = true },
            new() { Code = "PVT", Name = "生產驗證", Order = 5, Color = "#E6A23C", IsActive = true },
            new() { Code = "MP", Name = "量產", Order = 6, Color = "#909399", IsActive = true }
        };
    }
}


