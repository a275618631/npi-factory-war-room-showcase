namespace Core.Settings;

public record SettingItemDto(
    string Key,
    string DisplayName,
    string Category,
    string ValueType,
    string Value,
    string DefaultValue,
    bool IsOverridden,
    string Description);

public interface ISettingCenterService
{
    /// <summary>
    /// 取得設定值（若租戶有覆寫則回傳覆寫值，否則回傳平台預設值）
    /// </summary>
    Task<string> GetValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得型別轉換後的設定值
    /// </summary>
    Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 覆寫當前租戶的設定值（僅影響當前公司）
    /// </summary>
    Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得當前租戶的所有設定項目（包含預設值與覆寫狀態）
    /// </summary>
    Task<IReadOnlyList<SettingItemDto>> GetAllSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 將當前租戶的所有設定（含有效值）匯出為 JSON 字串
    /// </summary>
    Task<string> ExportSettingsJsonAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 將設定 JSON 匯入至當前租戶，使新公司的設定與匯出來源完全相同
    /// </summary>
    Task ImportSettingsJsonAsync(string json, CancellationToken cancellationToken = default);
}


