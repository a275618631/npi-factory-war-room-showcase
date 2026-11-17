using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using Core.Data;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Core.Import;

public class TableContractDefinition
{
    [JsonPropertyName("table_name")]
    public string TableName { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public List<TableContractField> Fields { get; set; } = new();
}

public class TableContractField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("example")]
    public string? Example { get; set; }
}

public class PersonImportResult
{
    public ImportBatch Batch { get; set; } = null!;
    public List<ImportError> Errors { get; set; } = new();
    public bool IsDuplicate { get; set; }
    public byte[]? ErrorReportExcel { get; set; }
}

public class PersonImportService : ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IFieldMappingService? _fieldMappingService;

    static PersonImportService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public PersonImportService(
        NpiDbContext dbContext,
        ICurrentTenant currentTenant,
        IFieldMappingService? fieldMappingService = null)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _fieldMappingService = fieldMappingService;
    }

    public async Task<PersonImportResult> ImportCsvAsync(
        Stream csvStream,
        string fileName,
        string? contractPath = null,
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await csvStream.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        var hashBytes = SHA256.HashData(fileBytes);
        var fileHash = Convert.ToHexStringLower(hashBytes);

        var existingBatch = await _dbContext.ImportBatches
            .FirstOrDefaultAsync(b => b.FileHash == fileHash && b.TableName == "stg_person", cancellationToken);

        if (existingBatch != null)
        {
            var existingErrors = await _dbContext.ImportErrors
                .Where(e => e.ImportBatchId == existingBatch.Id)
                .OrderBy(e => e.RowNumber)
                .ToListAsync(cancellationToken);

            return new PersonImportResult
            {
                Batch = existingBatch,
                Errors = existingErrors,
                IsDuplicate = true,
                ErrorReportExcel = existingErrors.Count > 0 ? GenerateErrorReportExcel(existingErrors) : null
            };
        }

        var contract = await LoadContractAsync(contractPath, cancellationToken);

        using var reader = new StreamReader(new MemoryStream(fileBytes), Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null,
        });

        if (!await csv.ReadAsync() || !csv.ReadHeader())
        {
            return await CreateEmptyBatchAsync(fileName, fileHash, cancellationToken);
        }

        var headerRecord = csv.HeaderRecord ?? Array.Empty<string>();
        var mappings = _fieldMappingService != null
            ? await _fieldMappingService.GetMappingAsync("stg_person", cancellationToken)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var columnToContract = ResolveColumnMapping(headerRecord, mappings, contract);
        var batchId = Guid.NewGuid();
        var tenantId = _currentTenant.Id;
        var errors = new List<ImportError>();

        // 檢查對應是否缺必填欄位
        if (!ValidateRequiredFieldMapping(columnToContract, contract, batchId, tenantId, errors))
        {
            return await SaveFailedBatchAsync(batchId, fileName, fileHash, tenantId, 0, errors, cancellationToken);
        }

        var validPersons = new List<StgPerson>();
        var totalRows = 0;

        while (await csv.ReadAsync())
        {
            totalRows++;
            var rowNumber = csv.Parser.Row;
            var rowValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headerRecord.Length; i++)
            {
                if (columnToContract.TryGetValue(i, out var contractFieldName))
                {
                    rowValues[contractFieldName] = csv.GetField(i)?.Trim();
                }
            }

            var (isValid, person) = ValidateAndCreatePerson(rowValues, contract, rowNumber, batchId, tenantId, errors);
            if (isValid && person != null)
            {
                validPersons.Add(person);
            }
        }

        return await FinalizeBatchAsync(batchId, fileName, fileHash, tenantId, totalRows, validPersons, errors, cancellationToken);
    }

    public async Task<PersonImportResult> ImportExcelAsync(
        Stream excelStream,
        string fileName,
        string? contractPath = null,
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await excelStream.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        var hashBytes = SHA256.HashData(fileBytes);
        var fileHash = Convert.ToHexStringLower(hashBytes);

        var existingBatch = await _dbContext.ImportBatches
            .FirstOrDefaultAsync(b => b.FileHash == fileHash && b.TableName == "stg_person", cancellationToken);

        if (existingBatch != null)
        {
            var existingErrors = await _dbContext.ImportErrors
                .Where(e => e.ImportBatchId == existingBatch.Id)
                .OrderBy(e => e.RowNumber)
                .ToListAsync(cancellationToken);

            return new PersonImportResult
            {
                Batch = existingBatch,
                Errors = existingErrors,
                IsDuplicate = true,
                ErrorReportExcel = existingErrors.Count > 0 ? GenerateErrorReportExcel(existingErrors) : null
            };
        }

        var contract = await LoadContractAsync(contractPath, cancellationToken);

        using var excelReader = ExcelReaderFactory.CreateReader(new MemoryStream(fileBytes));
        if (!excelReader.Read())
        {
            return await CreateEmptyBatchAsync(fileName, fileHash, cancellationToken);
        }

        var rawHeaders = new List<string>();
        for (int i = 0; i < excelReader.FieldCount; i++)
        {
            rawHeaders.Add(excelReader.GetValue(i)?.ToString()?.Trim() ?? string.Empty);
        }

        var mappings = _fieldMappingService != null
            ? await _fieldMappingService.GetMappingAsync("stg_person", cancellationToken)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var columnToContract = ResolveColumnMapping(rawHeaders, mappings, contract);
        var batchId = Guid.NewGuid();
        var tenantId = _currentTenant.Id;
        var errors = new List<ImportError>();

        // 檢查對應是否缺必填欄位
        if (!ValidateRequiredFieldMapping(columnToContract, contract, batchId, tenantId, errors))
        {
            return await SaveFailedBatchAsync(batchId, fileName, fileHash, tenantId, 0, errors, cancellationToken);
        }

        var validPersons = new List<StgPerson>();
        var totalRows = 0;
        var rowNumber = 1; // Header 是第 1 列

        while (excelReader.Read())
        {
            rowNumber++;
            totalRows++;
            var rowValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < excelReader.FieldCount; i++)
            {
                if (columnToContract.TryGetValue(i, out var contractFieldName))
                {
                    rowValues[contractFieldName] = excelReader.GetValue(i)?.ToString()?.Trim();
                }
            }

            var (isValid, person) = ValidateAndCreatePerson(rowValues, contract, rowNumber, batchId, tenantId, errors);
            if (isValid && person != null)
            {
                validPersons.Add(person);
            }
        }

        return await FinalizeBatchAsync(batchId, fileName, fileHash, tenantId, totalRows, validPersons, errors, cancellationToken);
    }

    private static Dictionary<int, string> ResolveColumnMapping(
        IReadOnlyList<string> headers,
        Dictionary<string, string> mappings,
        TableContractDefinition contract)
    {
        var result = new Dictionary<int, string>();
        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i]?.Trim();
            if (string.IsNullOrEmpty(h)) continue;

            if (mappings.TryGetValue(h, out var target) && !string.IsNullOrWhiteSpace(target))
            {
                result[i] = target.Trim().ToLowerInvariant();
            }
            else
            {
                var matched = contract.Fields.FirstOrDefault(f => f.Name.Equals(h, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    result[i] = matched.Name.ToLowerInvariant();
                }
            }
        }
        return result;
    }

    private static bool ValidateRequiredFieldMapping(
        Dictionary<int, string> columnToContract,
        TableContractDefinition contract,
        Guid batchId,
        Guid? tenantId,
        List<ImportError> errors)
    {
        var mappedContractFields = new HashSet<string>(columnToContract.Values, StringComparer.OrdinalIgnoreCase);
        var missingRequired = contract.Fields
            .Where(f => f.Required && !mappedContractFields.Contains(f.Name))
            .ToList();

        if (missingRequired.Count > 0)
        {
            foreach (var mf in missingRequired)
            {
                errors.Add(new ImportError(
                    Guid.NewGuid(),
                    batchId,
                    rowNumber: 1,
                    fieldName: mf.Name,
                    originalValue: null,
                    reason: $"對應缺必填欄位「{mf.Name}」",
                    suggestion: $"請在檔案中提供「{mf.Name}」（{mf.Description}）或設定欄位對應",
                    tenantId: tenantId));
            }
            return false;
        }

        return true;
    }

    private static (bool IsValid, StgPerson? Person) ValidateAndCreatePerson(
        Dictionary<string, string?> values,
        TableContractDefinition contract,
        int rowNumber,
        Guid batchId,
        Guid? tenantId,
        List<ImportError> errors)
    {
        var rowHasError = false;

        foreach (var field in contract.Fields)
        {
            values.TryGetValue(field.Name, out var val);

            if (field.Required && string.IsNullOrWhiteSpace(val))
            {
                rowHasError = true;
                errors.Add(new ImportError(
                    Guid.NewGuid(),
                    batchId,
                    rowNumber,
                    field.Name,
                    val,
                    $"必填欄位「{field.Name}」未填寫",
                    $"請填寫「{field.Name}」（{field.Description}）",
                    tenantId));
            }
            else if (field.Name.Equals("email", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(val))
            {
                if (!val.Contains('@') || !val.Contains('.'))
                {
                    rowHasError = true;
                    errors.Add(new ImportError(
                        Guid.NewGuid(),
                        batchId,
                        rowNumber,
                        field.Name,
                        val,
                        "電子郵件格式不正確",
                        "請輸入有效的電子郵件地址，例如 person@example.com",
                        tenantId));
                }
            }
        }

        if (rowHasError)
        {
            return (false, null);
        }

        values.TryGetValue("person_code", out var personCode);
        values.TryGetValue("display_name", out var displayName);
        values.TryGetValue("unit_code", out var unitCode);
        values.TryGetValue("email", out var email);
        values.TryGetValue("title", out var title);
        values.TryGetValue("phone_ext", out var phoneExt);
        values.TryGetValue("site_code", out var siteCode);
        values.TryGetValue("role_tags", out var roleTags);

        var person = new StgPerson(
            Guid.NewGuid(),
            personCode: personCode ?? string.Empty,
            displayName: displayName ?? string.Empty,
            unitCode: unitCode ?? string.Empty,
            tenantId: tenantId,
            importBatchId: batchId)
        {
            Email = email,
            Title = title,
            PhoneExt = phoneExt,
            SiteCode = siteCode,
            RoleTags = roleTags,
        };

        return (true, person);
    }

    private async Task<PersonImportResult> FinalizeBatchAsync(
        Guid batchId,
        string fileName,
        string fileHash,
        Guid? tenantId,
        int totalRows,
        List<StgPerson> validPersons,
        List<ImportError> errors,
        CancellationToken cancellationToken)
    {
        var failedRowCount = errors.Select(e => e.RowNumber).Distinct().Count();
        var isFailed = errors.Count > 0;

        var batch = new ImportBatch(batchId, "stg_person", fileName, fileHash, tenantId)
        {
            Status = isFailed ? ImportBatchStatus.Failed : ImportBatchStatus.Success,
            TotalRows = totalRows,
            SuccessRows = isFailed ? 0 : validPersons.Count,
            FailedRows = failedRowCount,
        };

        await _dbContext.ImportBatches.AddAsync(batch, cancellationToken);

        if (isFailed)
        {
            await _dbContext.ImportErrors.AddRangeAsync(errors, cancellationToken);
        }
        else
        {
            await _dbContext.StgPersons.AddRangeAsync(validPersons, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var excelReport = isFailed ? GenerateErrorReportExcel(errors) : null;

        return new PersonImportResult
        {
            Batch = batch,
            Errors = errors,
            ErrorReportExcel = excelReport,
            IsDuplicate = false
        };
    }

    private async Task<PersonImportResult> SaveFailedBatchAsync(
        Guid batchId,
        string fileName,
        string fileHash,
        Guid? tenantId,
        int totalRows,
        List<ImportError> errors,
        CancellationToken cancellationToken)
    {
        var batch = new ImportBatch(batchId, "stg_person", fileName, fileHash, tenantId)
        {
            Status = ImportBatchStatus.Failed,
            TotalRows = totalRows,
            SuccessRows = 0,
            FailedRows = errors.Select(e => e.RowNumber).Distinct().Count(),
        };

        await _dbContext.ImportBatches.AddAsync(batch, cancellationToken);
        await _dbContext.ImportErrors.AddRangeAsync(errors, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var excelReport = GenerateErrorReportExcel(errors);

        return new PersonImportResult
        {
            Batch = batch,
            Errors = errors,
            ErrorReportExcel = excelReport,
            IsDuplicate = false
        };
    }

    private async Task<PersonImportResult> CreateEmptyBatchAsync(
        string fileName,
        string fileHash,
        CancellationToken cancellationToken)
    {
        var emptyBatch = new ImportBatch(Guid.NewGuid(), "stg_person", fileName, fileHash, _currentTenant.Id)
        {
            Status = ImportBatchStatus.Success,
            TotalRows = 0,
            SuccessRows = 0,
            FailedRows = 0
        };
        await _dbContext.ImportBatches.AddAsync(emptyBatch, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new PersonImportResult { Batch = emptyBatch, IsDuplicate = false };
    }

    private static async Task<TableContractDefinition> LoadContractAsync(string? contractPath, CancellationToken cancellationToken)
    {
        var contractFile = ResolveContractPath(contractPath);
        var contractJson = await File.ReadAllTextAsync(contractFile, cancellationToken);
        return JsonSerializer.Deserialize<TableContractDefinition>(contractJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to read contract definition.");
    }

    public byte[] GenerateErrorReportExcel(List<ImportError> errors)
    {
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
        foreach (var err in errors.OrderBy(e => e.RowNumber))
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

    private static string ResolveContractPath(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            return customPath;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "contracts", "v1", "stg_person.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        var relative = Path.Combine("contracts", "v1", "stg_person.json");
        if (File.Exists(relative)) return Path.GetFullPath(relative);

        throw new FileNotFoundException("Cannot find contracts/v1/stg_person.json");
    }
}


