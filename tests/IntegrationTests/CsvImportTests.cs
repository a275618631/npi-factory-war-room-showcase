using ClosedXML.Excel;
using Core.Data;
using Core.Import;
using IntegrationTests.Isolation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

public class CsvImportTests : IsolationTestBase
{
    private static string GetTestDataPath(string fileName)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        if (File.Exists(basePath)) return basePath;

        // Fallback for direct source path
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(current, "tests", "IntegrationTests", "TestData", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        throw new FileNotFoundException($"Cannot find test data file: {fileName}");
    }

    [Fact]
    [Trait("Category", "")]
    public async Task ValidCsv_ImportsSuccessfully_StatusIsSuccess_RecordCountCorrect()
    {
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var importService = sp.GetRequiredService<PersonImportService>();
            var filePath = GetTestDataPath("valid_persons.csv");
            await using var stream = File.OpenRead(filePath);

            var result = await importService.ImportCsvAsync(stream, "valid_persons.csv");

            Assert.False(result.IsDuplicate);
            Assert.Equal("成功", result.Batch.Status);
            Assert.Equal(3, result.Batch.TotalRows);
            Assert.Equal(3, result.Batch.SuccessRows);
            Assert.Equal(0, result.Batch.FailedRows);
            Assert.Empty(result.Errors);
            Assert.Null(result.ErrorReportExcel);

            var savedPersons = await dbContext.StgPersons
                .Where(p => p.ImportBatchId == result.Batch.Id)
                .OrderBy(p => p.PersonCode)
                .ToListAsync();

            Assert.Equal(3, savedPersons.Count);
            Assert.Equal("E1001", savedPersons[0].PersonCode);
            Assert.Equal("Example Person 1", savedPersons[0].DisplayName);
            Assert.Equal("RD", savedPersons[0].UnitCode);
            Assert.Equal("E1002", savedPersons[1].PersonCode);
            Assert.Equal("E1003", savedPersons[2].PersonCode);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task InvalidCsv_With2RowsMissingRequiredFields_BatchFails_ErrorReportLists2Rows_StagingTableEmpty()
    {
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var importService = sp.GetRequiredService<PersonImportService>();
            var filePath = GetTestDataPath("invalid_persons.csv");
            await using var stream = File.OpenRead(filePath);

            var result = await importService.ImportCsvAsync(stream, "invalid_persons.csv");

            Assert.False(result.IsDuplicate);
            Assert.Equal("失敗", result.Batch.Status);
            Assert.Equal(4, result.Batch.TotalRows);
            Assert.Equal(0, result.Batch.SuccessRows);
            Assert.Equal(2, result.Batch.FailedRows);
            Assert.Equal(2, result.Errors.Count);

            // Error 1: row 3, missing person_code
            var err1 = result.Errors.First(e => e.RowNumber == 3);
            Assert.Equal("person_code", err1.FieldName);
            Assert.True(string.IsNullOrEmpty(err1.OriginalValue));
            Assert.Contains("必填", err1.Reason);

            // Error 2: row 4, missing display_name
            var err2 = result.Errors.First(e => e.RowNumber == 4);
            Assert.Equal("display_name", err2.FieldName);
            Assert.True(string.IsNullOrEmpty(err2.OriginalValue));
            Assert.Contains("必填", err2.Reason);

            // Staging table must NOT have any rows written
            var savedPersons = await dbContext.StgPersons
                .Where(p => p.ImportBatchId == result.Batch.Id)
                .ToListAsync();
            Assert.Empty(savedPersons);

            // DB should store the 2 import errors
            var dbErrors = await dbContext.ImportErrors
                .Where(e => e.ImportBatchId == result.Batch.Id)
                .ToListAsync();
            Assert.Equal(2, dbErrors.Count);

            // Verify ClosedXML Excel Error Report
            Assert.NotNull(result.ErrorReportExcel);
            Assert.NotEmpty(result.ErrorReportExcel);

            using var memoryStream = new MemoryStream(result.ErrorReportExcel);
            using var workbook = new XLWorkbook(memoryStream);
            var worksheet = workbook.Worksheets.FirstOrDefault(w => w.Name == "匯入錯誤報表");
            Assert.NotNull(worksheet);

            // Check header
            Assert.Equal("列號", worksheet.Cell(1, 1).GetString());
            Assert.Equal("欄位", worksheet.Cell(1, 2).GetString());
            Assert.Equal("原值", worksheet.Cell(1, 3).GetString());
            Assert.Equal("原因", worksheet.Cell(1, 4).GetString());
            Assert.Equal("建議修法", worksheet.Cell(1, 5).GetString());

            // Check error rows
            Assert.Equal("3", worksheet.Cell(2, 1).GetString());
            Assert.Equal("person_code", worksheet.Cell(2, 2).GetString());
            Assert.Contains("必填", worksheet.Cell(2, 4).GetString());

            Assert.Equal("4", worksheet.Cell(3, 1).GetString());
            Assert.Equal("display_name", worksheet.Cell(3, 2).GetString());
            Assert.Contains("必填", worksheet.Cell(3, 4).GetString());
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task DuplicateCsv_ResendingSameFile_DoesNotDuplicateWrite()
    {
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var importService = sp.GetRequiredService<PersonImportService>();
            var filePath = GetTestDataPath("valid_persons.csv");

            // First import
            await using (var stream1 = File.OpenRead(filePath))
            {
                var result1 = await importService.ImportCsvAsync(stream1, "valid_persons.csv");
                Assert.False(result1.IsDuplicate);
                Assert.Equal("成功", result1.Batch.Status);
                Assert.Equal(3, result1.Batch.SuccessRows);
            }

            var countAfterFirstImport = await dbContext.StgPersons.CountAsync();
            Assert.Equal(3, countAfterFirstImport);

            // Resend the exact same file
            await using (var stream2 = File.OpenRead(filePath))
            {
                var result2 = await importService.ImportCsvAsync(stream2, "valid_persons.csv");
                Assert.True(result2.IsDuplicate);
                Assert.Equal("成功", result2.Batch.Status);
            }

            // Record count in staging table remains unchanged (no duplicate write)
            var countAfterResend = await dbContext.StgPersons.CountAsync();
            Assert.Equal(3, countAfterResend);

            // Batch record count with this file hash remains 1
            var batchCount = await dbContext.ImportBatches.CountAsync();
            Assert.Equal(1, batchCount);
        });
    }
}



