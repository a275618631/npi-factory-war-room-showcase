using ClosedXML.Excel;
using Core.Data;
using Core.Import;
using IntegrationTests.Isolation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Import;

public class ExcelImportTests : IsolationTestBase
{
    private static MemoryStream CreateExcelStream(string[] headers, List<string[]> rows)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");

        for (int col = 0; col < headers.Length; col++)
        {
            worksheet.Cell(1, col + 1).Value = headers[col];
        }

        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].Length; c++)
            {
                worksheet.Cell(r + 2, c + 1).Value = rows[r][c];
            }
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    [Trait("Category", "")]
    public async Task ExcelImport_WithCustomFieldMappings_ImportsSuccessfully()
    {
        // 驗收條件：欄位名稱是「員工編號、姓名、部門」的 Excel，套用對應後匯入成功
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var mappingService = sp.GetRequiredService<IFieldMappingService>();
            var importService = sp.GetRequiredService<PersonImportService>();

            // 1. 設定公司自訂欄位對應（公司欄位名稱 → 契約欄位）
            var mappings = new Dictionary<string, string>
            {
                ["員工編號"] = "person_code",
                ["姓名"] = "display_name",
                ["部門"] = "unit_code",
                ["信箱"] = "email",
                ["分機"] = "phone_ext"
            };
            await mappingService.SetMappingAsync("stg_person", mappings);

            // 2. 建立 Excel 檔案（使用公司自訂欄位名稱）
            var headers = new[] { "員工編號", "姓名", "部門", "信箱", "分機" };
            var rows = new List<string[]>
            {
                new[] { "E2001", "李大仁", "RD", "daren@example.com", "8801" },
                new[] { "E2002", "程又青", "PM", "qing@example.com", "8802" }
            };

            await using var excelStream = CreateExcelStream(headers, rows);

            // 3. 執行 Excel 匯入
            var result = await importService.ImportExcelAsync(excelStream, "employees_custom.xlsx");

            // 4. 驗證結果
            Assert.False(result.IsDuplicate);
            Assert.Equal("成功", result.Batch.Status);
            Assert.Equal(2, result.Batch.TotalRows);
            Assert.Equal(2, result.Batch.SuccessRows);
            Assert.Equal(0, result.Batch.FailedRows);
            Assert.Empty(result.Errors);
            Assert.Null(result.ErrorReportExcel);

            // 驗證寫入 StgPersons
            var savedPersons = await dbContext.StgPersons
                .AsNoTracking()
                .Where(p => p.ImportBatchId == result.Batch.Id)
                .OrderBy(p => p.PersonCode)
                .ToListAsync();

            Assert.Equal(2, savedPersons.Count);
            Assert.Equal("E2001", savedPersons[0].PersonCode);
            Assert.Equal("李大仁", savedPersons[0].DisplayName);
            Assert.Equal("RD", savedPersons[0].UnitCode);
            Assert.Equal("daren@example.com", savedPersons[0].Email);
            Assert.Equal("8801", savedPersons[0].PhoneExt);

            Assert.Equal("E2002", savedPersons[1].PersonCode);
            Assert.Equal("程又青", savedPersons[1].DisplayName);
            Assert.Equal("PM", savedPersons[1].UnitCode);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task ExcelImport_MissingRequiredFieldMapping_FailsEntireBatch_ListsMissingFieldInReport()
    {
        // 驗收條件：對應缺必填欄位時，整批失敗，錯誤報表寫明缺哪個欄位
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var mappingService = sp.GetRequiredService<IFieldMappingService>();
            var importService = sp.GetRequiredService<PersonImportService>();

            // 1. 設定欄位對應（故意缺少必填欄位 unit_code / 部門 的對應）
            var mappings = new Dictionary<string, string>
            {
                ["員工編號"] = "person_code",
                ["姓名"] = "display_name"
                // 缺少 unit_code
            };
            await mappingService.SetMappingAsync("stg_person", mappings);

            // 2. 建立 Excel，只有員工編號與姓名，缺部門
            var headers = new[] { "員工編號", "姓名" };
            var rows = new List<string[]>
            {
                new[] { "E3001", "陳小華" },
                new[] { "E3002", "林大榮" }
            };

            await using var excelStream = CreateExcelStream(headers, rows);

            // 3. 執行匯入
            var result = await importService.ImportExcelAsync(excelStream, "missing_dept.xlsx");

            // 4. 驗證整批失敗
            Assert.False(result.IsDuplicate);
            Assert.Equal("失敗", result.Batch.Status);
            Assert.Equal(0, result.Batch.SuccessRows);
            Assert.NotEmpty(result.Errors);

            // 錯誤報表寫明缺哪個欄位
            var missingUnitCodeError = result.Errors.FirstOrDefault(e => e.FieldName == "unit_code");
            Assert.NotNull(missingUnitCodeError);
            Assert.Contains("必填", missingUnitCodeError.Reason);

            // Staging Table 不得有任何資料寫入
            var savedPersons = await dbContext.StgPersons
                .AsNoTracking()
                .Where(p => p.ImportBatchId == result.Batch.Id)
                .ToListAsync();
            Assert.Empty(savedPersons);

            // 錯誤報表 Excel 產生且包含錯誤內容
            Assert.NotNull(result.ErrorReportExcel);
            using var errorStream = new MemoryStream(result.ErrorReportExcel);
            using var errorWorkbook = new XLWorkbook(errorStream);
            var sheet = errorWorkbook.Worksheets.FirstOrDefault(w => w.Name == "匯入錯誤報表");
            Assert.NotNull(sheet);
            Assert.Equal("unit_code", sheet.Cell(2, 2).GetString());
            Assert.Contains("必填", sheet.Cell(2, 4).GetString());
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task CsvAndExcel_ShareSameMappingConfiguration()
    {
        // 範圍：CSV 和 Excel 共用同一套對應設定
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var mappingService = sp.GetRequiredService<IFieldMappingService>();
            var importService = sp.GetRequiredService<PersonImportService>();

            // 1. 設定共用對應規則
            var mappings = new Dictionary<string, string>
            {
                ["工號"] = "person_code",
                ["中文名"] = "display_name",
                ["單位"] = "unit_code"
            };
            await mappingService.SetMappingAsync("stg_person", mappings);

            // 2. 以 CSV 格式測試同一套對應
            var csvContent = "工號,中文名,單位\nE4001,王大衛,QA\n";
            using var csvStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent));
            var csvResult = await importService.ImportCsvAsync(csvStream, "shared_mapping.csv");

            Assert.Equal("成功", csvResult.Batch.Status);
            Assert.Equal(1, csvResult.Batch.SuccessRows);

            var savedCsvPerson = await dbContext.StgPersons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonCode == "E4001" && p.ImportBatchId == csvResult.Batch.Id);
            Assert.NotNull(savedCsvPerson);
            Assert.Equal("王大衛", savedCsvPerson.DisplayName);
            Assert.Equal("QA", savedCsvPerson.UnitCode);

            // 3. 以 Excel 格式測試同一套對應
            var excelHeaders = new[] { "工號", "中文名", "單位" };
            var excelRows = new List<string[]>
            {
                new[] { "E4002", "趙敏", "RD" }
            };
            await using var excelStream = CreateExcelStream(excelHeaders, excelRows);
            var excelResult = await importService.ImportExcelAsync(excelStream, "shared_mapping.xlsx");

            Assert.Equal("成功", excelResult.Batch.Status);
            Assert.Equal(1, excelResult.Batch.SuccessRows);

            var savedExcelPerson = await dbContext.StgPersons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonCode == "E4002" && p.ImportBatchId == excelResult.Batch.Id);
            Assert.NotNull(savedExcelPerson);
            Assert.Equal("趙敏", savedExcelPerson.DisplayName);
            Assert.Equal("RD", savedExcelPerson.UnitCode);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task TenantIsolation_MappingSettingsAreIsolatedBetweenTenants()
    {
        // 租戶隔離：TenantA 的自訂對應不影響 TenantB
        await ExecuteScopedAsync(TenantAId, async (dbContextA, spA) =>
        {
            var mappingServiceA = spA.GetRequiredService<IFieldMappingService>();
            await mappingServiceA.SetMappingAsync("stg_person", new Dictionary<string, string>
            {
                ["員工編號"] = "person_code"
            });
        });

        await ExecuteScopedAsync(TenantBId, async (dbContextB, spB) =>
        {
            var mappingServiceB = spB.GetRequiredService<IFieldMappingService>();
            var mappingsB = await mappingServiceB.GetMappingAsync("stg_person");

            // TenantB 尚未設定，不應讀到 TenantA 的對應
            Assert.False(mappingsB.ContainsKey("員工編號"));
        });
    }
}


