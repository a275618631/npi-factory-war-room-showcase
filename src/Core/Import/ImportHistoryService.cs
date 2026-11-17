using ClosedXML.Excel;
using Core.Data;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Core.Import;

public record ImportBatchDto(
    Guid Id,
    string TableName,
    string FileName,
    string Status,
    int TotalRows,
    int SuccessRows,
    int FailedRows,
    string ImportMethod,
    DateTime CreatedAt);

public record ImportErrorDto(
    Guid Id,
    Guid ImportBatchId,
    int RowNumber,
    string FieldName,
    string? OriginalValue,
    string Reason,
    string? Suggestion,
    DateTime CreatedAt);

public record ImportBatchDetailDto(
    ImportBatchDto Batch,
    List<ImportErrorDto> Errors);

public interface IImportHistoryService
{
    Task<List<ImportBatchDto>> GetBatchesAsync(int take = 50, CancellationToken cancellationToken = default);
    Task<ImportBatchDetailDto?> GetBatchDetailAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<byte[]?> DownloadErrorReportExcelAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public class ImportHistoryService : IImportHistoryService, ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public ImportHistoryService(NpiDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    public async Task<List<ImportBatchDto>> GetBatchesAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.Id;
        var query = _dbContext.ImportBatches.AsNoTracking();

        if (tenantId.HasValue)
        {
            query = query.Where(b => b.TenantId == tenantId.Value);
        }

        var batches = await query
            .OrderByDescending(b => b.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return batches.Select(b => new ImportBatchDto(
            b.Id,
            b.TableName,
            b.FileName,
            b.Status,
            b.TotalRows,
            b.SuccessRows,
            b.FailedRows,
            DetermineImportMethod(b.FileName),
            b.CreatedAt
        )).ToList();
    }

    public async Task<ImportBatchDetailDto?> GetBatchDetailAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.Id;
        var query = _dbContext.ImportBatches.AsNoTracking();

        if (tenantId.HasValue)
        {
            query = query.Where(b => b.TenantId == tenantId.Value);
        }

        var batch = await query.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch == null)
        {
            return null;
        }

        var errorsQuery = _dbContext.ImportErrors.AsNoTracking().Where(e => e.ImportBatchId == batchId);
        if (tenantId.HasValue)
        {
            errorsQuery = errorsQuery.Where(e => e.TenantId == tenantId.Value);
        }

        var errors = await errorsQuery
            .OrderBy(e => e.RowNumber)
            .Select(e => new ImportErrorDto(
                e.Id,
                e.ImportBatchId,
                e.RowNumber,
                e.FieldName,
                e.OriginalValue,
                e.Reason,
                e.Suggestion,
                e.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        var batchDto = new ImportBatchDto(
            batch.Id,
            batch.TableName,
            batch.FileName,
            batch.Status,
            batch.TotalRows,
            batch.SuccessRows,
            batch.FailedRows,
            DetermineImportMethod(batch.FileName),
            batch.CreatedAt
        );

        return new ImportBatchDetailDto(batchDto, errors);
    }

    public async Task<byte[]?> DownloadErrorReportExcelAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var detail = await GetBatchDetailAsync(batchId, cancellationToken);
        if (detail == null)
        {
            return null;
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("匯入錯誤報表");

        worksheet.Cell(1, 1).Value = "列號";
        worksheet.Cell(1, 2).Value = "欄位";
        worksheet.Cell(1, 3).Value = "原值";
        worksheet.Cell(1, 4).Value = "原因";
        worksheet.Cell(1, 5).Value = "建議修法";

        var headerRange = worksheet.Range(1, 1, 1, 5);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        var row = 2;
        foreach (var err in detail.Errors.OrderBy(e => e.RowNumber))
        {
            worksheet.Cell(row, 1).Value = err.RowNumber;
            worksheet.Cell(row, 2).Value = err.FieldName;
            worksheet.Cell(row, 3).Value = err.OriginalValue ?? string.Empty;
            worksheet.Cell(row, 4).Value = err.Reason;
            worksheet.Cell(row, 5).Value = err.Suggestion ?? string.Empty;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static string DetermineImportMethod(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "REST API 匯入";
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".xlsx" || ext == ".xls")
        {
            return "Excel 上傳";
        }
        if (ext == ".csv")
        {
            return "CSV 上傳";
        }
        if (fileName.StartsWith("api-batch", StringComparison.OrdinalIgnoreCase))
        {
            return "REST API 匯入";
        }

        return "批次檔案匯入";
    }
}


