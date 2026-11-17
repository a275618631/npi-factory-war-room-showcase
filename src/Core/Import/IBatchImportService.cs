using System.Text.Json;

namespace Core.Import;

public class BatchImportPayload
{
    public Guid BatchId { get; set; } = Guid.NewGuid();
    public List<JsonElement> Rows { get; set; } = new();
}

public class BatchImportResponse
{
    public Guid BatchId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string Status { get; set; } = ImportBatchStatus.Success;
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public bool IsDuplicate { get; set; }
    public List<string> Errors { get; set; } = new();
}

public interface IBatchImportService
{
    /// <summary>
    /// 透過 API Key 驗證並取得所屬租戶 ID（若金鑰無效或已停用則回傳 null）
    /// </summary>
    Task<Guid?> AuthenticateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 執行批次資料匯入（支援冪等性防重複匯入）
    /// </summary>
    Task<BatchImportResponse> ImportBatchAsync(
        string tableName,
        BatchImportPayload payload,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}


