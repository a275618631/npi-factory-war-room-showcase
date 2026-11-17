using Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Core.Import;

public class BatchImportService : IBatchImportService, ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public BatchImportService(NpiDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    public async Task<Guid?> AuthenticateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        // API Key 驗證：每家公司各自的金鑰，已停用的金鑰回傳 null
        var keyRecord = await _dbContext.TenantApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == apiKey, cancellationToken);

        if (keyRecord == null || !keyRecord.IsActive)
        {
            return null;
        }

        return keyRecord.TenantId;
    }

    public async Task<BatchImportResponse> ImportBatchAsync(
        string tableName,
        BatchImportPayload payload,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = tenantId ?? _currentTenant.Id;
        var normalizedTable = NormalizeTableName(tableName);

        // 驗收條件 1：同一個批次編號送兩次，第二次回傳第一次的結果，不重複寫入
        var existingBatch = await _dbContext.ImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == payload.BatchId, cancellationToken);

        if (existingBatch != null)
        {
            return new BatchImportResponse
            {
                BatchId = existingBatch.Id,
                TableName = existingBatch.TableName,
                Status = existingBatch.Status,
                TotalRows = existingBatch.TotalRows,
                SuccessRows = existingBatch.SuccessRows,
                FailedRows = existingBatch.FailedRows,
                IsDuplicate = true
            };
        }

        var totalRows = payload.Rows.Count;
        var successRows = 0;
        var failedRows = 0;
        var errors = new List<string>();

        for (int i = 0; i < payload.Rows.Count; i++)
        {
            var row = payload.Rows[i];
            var rowNumber = i + 1;

            if (normalizedTable == "stg_org_unit")
            {
                var unitCode = GetPropertyString(row, "unit_code");
                var unitName = GetPropertyString(row, "unit_name");
                var parentCode = GetPropertyString(row, "parent_code");
                var team = GetPropertyString(row, "team");
                var managerPersonCode = GetPropertyString(row, "manager_person_code");

                if (string.IsNullOrWhiteSpace(unitCode) || string.IsNullOrWhiteSpace(unitName))
                {
                    failedRows++;
                    var reason = $"第 {rowNumber} 列缺少必填欄位 unit_code 或 unit_name";
                    errors.Add(reason);
                    await RecordErrorAsync(payload.BatchId, rowNumber, "unit_code", unitCode ?? string.Empty, reason, effectiveTenantId, cancellationToken);
                    continue;
                }

                var entity = new StgOrgUnit(
                    Guid.NewGuid(),
                    unitCode,
                    unitName,
                    parentCode,
                    team,
                    managerPersonCode,
                    effectiveTenantId,
                    payload.BatchId);

                await _dbContext.StgOrgUnits.AddAsync(entity, cancellationToken);
                successRows++;
            }
            else if (normalizedTable == "stg_person")
            {
                var personCode = GetPropertyString(row, "person_code");
                var displayName = GetPropertyString(row, "display_name");
                var unitCode = GetPropertyString(row, "unit_code");
                var email = GetPropertyString(row, "email");
                var title = GetPropertyString(row, "title");
                var phoneExt = GetPropertyString(row, "phone_ext");
                var siteCode = GetPropertyString(row, "site_code");
                var roleTags = GetPropertyString(row, "role_tags");

                if (string.IsNullOrWhiteSpace(personCode) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(unitCode))
                {
                    failedRows++;
                    var reason = $"第 {rowNumber} 列缺少必填欄位 person_code、display_name 或 unit_code";
                    errors.Add(reason);
                    await RecordErrorAsync(payload.BatchId, rowNumber, "person_code", personCode ?? string.Empty, reason, effectiveTenantId, cancellationToken);
                    continue;
                }

                var entity = new StgPerson(
                    Guid.NewGuid(),
                    personCode,
                    displayName,
                    unitCode,
                    effectiveTenantId,
                    payload.BatchId)
                {
                    Email = email,
                    Title = title,
                    PhoneExt = phoneExt,
                    SiteCode = siteCode,
                    RoleTags = roleTags
                };

                await _dbContext.StgPersons.AddAsync(entity, cancellationToken);
                successRows++;
            }
            else if (normalizedTable == "stg_project")
            {
                var projectCode = GetPropertyString(row, "project_code");
                var projectName = GetPropertyString(row, "project_name");
                var pmPersonCode = GetPropertyString(row, "pm_person_code");
                var currentStageCode = GetPropertyString(row, "current_stage_code");
                var currentBuild = GetPropertyString(row, "current_build");
                var plannedQtyStr = GetPropertyString(row, "planned_qty");
                var customerRef = GetPropertyString(row, "customer_ref");

                if (string.IsNullOrWhiteSpace(projectCode) || string.IsNullOrWhiteSpace(projectName) ||
                    string.IsNullOrWhiteSpace(pmPersonCode) || string.IsNullOrWhiteSpace(currentStageCode))
                {
                    failedRows++;
                    var reason = $"第 {rowNumber} 列缺少專案必填欄位";
                    errors.Add(reason);
                    await RecordErrorAsync(payload.BatchId, rowNumber, "project_code", projectCode ?? string.Empty, reason, effectiveTenantId, cancellationToken);
                    continue;
                }

                int? plannedQty = int.TryParse(plannedQtyStr, out var q) ? q : null;

                var entity = new StgProject(
                    Guid.NewGuid(),
                    projectCode,
                    projectName,
                    pmPersonCode,
                    currentStageCode,
                    effectiveTenantId)
                {
                    ImportBatchId = payload.BatchId,
                    CurrentBuild = currentBuild,
                    PlannedQty = plannedQty,
                    CustomerRef = customerRef
                };

                await _dbContext.StgProjects.AddAsync(entity, cancellationToken);
                successRows++;
            }
            else
            {
                failedRows++;
                var reason = $"不支援的匯入資料表：{normalizedTable}";
                errors.Add(reason);
                await RecordErrorAsync(payload.BatchId, rowNumber, "table", normalizedTable, reason, effectiveTenantId, cancellationToken);
            }
        }

        var status = (failedRows > 0 && successRows == 0) ? ImportBatchStatus.Failed : ImportBatchStatus.Success;

        var batch = new ImportBatch(
            payload.BatchId,
            normalizedTable,
            $"REST_API_BATCH_{payload.BatchId}",
            payload.BatchId.ToString("N"),
            effectiveTenantId)
        {
            Status = status,
            TotalRows = totalRows,
            SuccessRows = successRows,
            FailedRows = failedRows
        };

        await _dbContext.ImportBatches.AddAsync(batch, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new BatchImportResponse
        {
            BatchId = batch.Id,
            TableName = normalizedTable,
            Status = status,
            TotalRows = totalRows,
            SuccessRows = successRows,
            FailedRows = failedRows,
            IsDuplicate = false,
            Errors = errors
        };
    }

    private static string NormalizeTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return string.Empty;
        var lower = tableName.Trim().ToLowerInvariant();
        return lower.StartsWith("stg_") ? lower : $"stg_{lower}";
    }

    private static string? GetPropertyString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }
        }

        return null;
    }

    private async Task RecordErrorAsync(
        Guid batchId,
        int rowNumber,
        string fieldName,
        string originalValue,
        string reason,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var error = new ImportError(
            Guid.NewGuid(),
            batchId,
            rowNumber,
            fieldName,
            originalValue,
            reason,
            "請確認必填欄位格式後重新送出",
            tenantId);

        await _dbContext.ImportErrors.AddAsync(error, cancellationToken);
    }
}


