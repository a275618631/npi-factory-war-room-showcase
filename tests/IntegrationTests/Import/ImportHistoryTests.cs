using ClosedXML.Excel;
using Core.Data;
using Core.Import;
using IntegrationTests.Isolation;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests.Import;

public class ImportHistoryTests : IsolationTestBase
{
    [Fact]
    [Trait("Category", "")]
    public async Task ImportWithErrors_ListsErrorsAndExcelMatchesContent()
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();

        var batchId = Guid.NewGuid();
        using (currentTenant.Change(TenantAId))
        {
            var batch = new ImportBatch(batchId, "stg_person", "person_test.csv", "hash-err-123", TenantAId)
            {
                TotalRows = 5,
                SuccessRows = 3,
                FailedRows = 2,
                Status = ImportBatchStatus.Failed
            };

            var error1 = new ImportError(
                Guid.NewGuid(),
                batchId,
                rowNumber: 2,
                fieldName: "person_code",
                originalValue: "",
                reason: "工號必填不能為空",
                suggestion: "請填入公司唯一工號",
                tenantId: TenantAId);

            var error2 = new ImportError(
                Guid.NewGuid(),
                batchId,
                rowNumber: 4,
                fieldName: "email",
                originalValue: "invalid-email-format",
                reason: "電子郵件格式不正確",
                suggestion: "請檢查是否包含 @ 與網域名稱",
                tenantId: TenantAId);

            await dbContext.ImportBatches.AddAsync(batch);
            await dbContext.ImportErrors.AddRangeAsync(error1, error2);
            await dbContext.SaveChangesAsync();
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("__tenant", TenantAId.ToString());

        // 1. 取得批次清單
        var batchesRes = await client.GetAsync("/api/import/batches");
        Assert.True(batchesRes.IsSuccessStatusCode);
        var batches = await batchesRes.Content.ReadFromJsonAsync<List<ImportBatchDto>>();
        Assert.NotNull(batches);
        Assert.Contains(batches, b => b.Id == batchId && b.FailedRows == 2);

        // 2. 點進去看錯誤明細
        var detailRes = await client.GetAsync($"/api/import/batches/{batchId}");
        Assert.True(detailRes.IsSuccessStatusCode);
        var detail = await detailRes.Content.ReadFromJsonAsync<ImportBatchDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(2, detail.Errors.Count);

        var firstError = detail.Errors.First(e => e.RowNumber == 2);
        Assert.Equal("person_code", firstError.FieldName);
        Assert.Equal("工號必填不能為空", firstError.Reason);

        var secondError = detail.Errors.First(e => e.RowNumber == 4);
        Assert.Equal("email", secondError.FieldName);
        Assert.Equal("invalid-email-format", secondError.OriginalValue);

        // 3. 下載錯誤報表 Excel 並核對內容
        var excelRes = await client.GetAsync($"/api/import/batches/{batchId}/error-report");
        Assert.True(excelRes.IsSuccessStatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelRes.Content.Headers.ContentType?.MediaType);

        var excelBytes = await excelRes.Content.ReadAsByteArrayAsync();
        Assert.True(excelBytes.Length > 0);

        using var ms = new MemoryStream(excelBytes);
        using var workbook = new XLWorkbook(ms);
        var sheet = workbook.Worksheet(1);
        Assert.Equal("匯入錯誤報表", sheet.Name);

        // 檢查標題列
        Assert.Equal("列號", sheet.Cell(1, 1).GetString());
        Assert.Equal("欄位", sheet.Cell(1, 2).GetString());
        Assert.Equal("原值", sheet.Cell(1, 3).GetString());
        Assert.Equal("原因", sheet.Cell(1, 4).GetString());
        Assert.Equal("建議修法", sheet.Cell(1, 5).GetString());

        // 檢查資料列內容與畫面明細一致
        Assert.Equal(2, sheet.Cell(2, 1).GetValue<int>());
        Assert.Equal("person_code", sheet.Cell(2, 2).GetString());
        Assert.Equal("工號必填不能為空", sheet.Cell(2, 4).GetString());
        Assert.Equal("請填入公司唯一工號", sheet.Cell(2, 5).GetString());

        Assert.Equal(4, sheet.Cell(3, 1).GetValue<int>());
        Assert.Equal("email", sheet.Cell(3, 2).GetString());
        Assert.Equal("invalid-email-format", sheet.Cell(3, 3).GetString());
        Assert.Equal("電子郵件格式不正確", sheet.Cell(3, 4).GetString());
    }

    [Fact]
    [Trait("Category", "")]
    public async Task TenantIsolation_CompanyBCannotSeeOrDownloadCompanyABatches()
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();

        var batchAId = Guid.NewGuid();

        // 公司 A 建立一筆匯入批次
        using (currentTenant.Change(TenantAId))
        {
            var batchA = new ImportBatch(batchAId, "stg_org_unit", "org_a.csv", "hash-a-999", TenantAId)
            {
                TotalRows = 10,
                SuccessRows = 10,
                FailedRows = 0,
                Status = ImportBatchStatus.Success
            };
            await dbContext.ImportBatches.AddAsync(batchA);
            await dbContext.SaveChangesAsync();
        }

        // 以公司 B 的身份呼叫 API
        var clientB = Factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("__tenant", TenantBId.ToString());

        // 驗收條件：B 公司的管理員看不到 A 公司的匯入紀錄
        var batchesResB = await clientB.GetAsync("/api/import/batches");
        Assert.True(batchesResB.IsSuccessStatusCode);
        var batchesB = await batchesResB.Content.ReadFromJsonAsync<List<ImportBatchDto>>();
        Assert.NotNull(batchesB);
        Assert.DoesNotContain(batchesB, b => b.Id == batchAId);

        // B 公司試圖取得 A 公司批次明細 -> 404
        var detailResB = await clientB.GetAsync($"/api/import/batches/{batchAId}");
        Assert.Equal(HttpStatusCode.NotFound, detailResB.StatusCode);

        // B 公司試圖下載 A 公司錯誤報表 -> 404
        var downloadResB = await clientB.GetAsync($"/api/import/batches/{batchAId}/error-report");
        Assert.Equal(HttpStatusCode.NotFound, downloadResB.StatusCode);
    }
}


