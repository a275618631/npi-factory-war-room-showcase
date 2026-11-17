using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using CsvHelper.Configuration;

namespace MockData;

public record ContractValidationError(int RowNumber, string FieldName, string Message);

public class ContractSchema
{
    [JsonPropertyName("table_name")]
    public string TableName { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public List<ContractFieldSchema> Fields { get; set; } = new();
}

public class ContractFieldSchema
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
    public object? Example { get; set; }
}

public static class ContractValidator
{
    public static List<ContractValidationError> ValidateCsvContent(string csvContent, string contractJsonPath)
    {
        var errors = new List<ContractValidationError>();

        if (!File.Exists(contractJsonPath))
        {
            throw new FileNotFoundException($"Contract definition file not found: {contractJsonPath}");
        }

        var contractJson = File.ReadAllText(contractJsonPath);
        var contract = JsonSerializer.Deserialize<ContractSchema>(contractJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Failed to deserialize contract from {contractJsonPath}");

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null,
        });

        if (!csv.Read() || !csv.ReadHeader())
        {
            errors.Add(new ContractValidationError(0, string.Empty, "CSV file is empty or missing headers."));
            return errors;
        }

        var headerRecord = csv.HeaderRecord ?? Array.Empty<string>();
        var headerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in headerRecord)
        {
            headerMap[h.Trim()] = h.Trim();
        }

        // Check required fields in header
        foreach (var field in contract.Fields.Where(f => f.Required))
        {
            if (!headerMap.ContainsKey(field.Name))
            {
                errors.Add(new ContractValidationError(0, field.Name, $"Required field '{field.Name}' is missing from CSV header."));
            }
        }

        var rowNumber = 1;
        while (csv.Read())
        {
            rowNumber++;
            foreach (var field in contract.Fields)
            {
                string? val = null;
                if (headerMap.TryGetValue(field.Name, out var actualHeader))
                {
                    val = csv.GetField(actualHeader);
                }

                if (field.Required && string.IsNullOrWhiteSpace(val))
                {
                    errors.Add(new ContractValidationError(rowNumber, field.Name, $"Required field '{field.Name}' is empty or whitespace."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(val))
                {
                    continue;
                }

                switch (field.Type.ToLowerInvariant())
                {
                    case "date":
                        if (!DateOnly.TryParseExact(val, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        {
                            errors.Add(new ContractValidationError(rowNumber, field.Name, $"Field '{field.Name}' value '{val}' is not a valid date format (yyyy-MM-dd)."));
                        }
                        break;
                    case "integer":
                        if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        {
                            errors.Add(new ContractValidationError(rowNumber, field.Name, $"Field '{field.Name}' value '{val}' is not a valid integer."));
                        }
                        break;
                    case "boolean":
                        if (!val.Equals("true", StringComparison.OrdinalIgnoreCase) && !val.Equals("false", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add(new ContractValidationError(rowNumber, field.Name, $"Field '{field.Name}' value '{val}' is not a valid boolean (true/false)."));
                        }
                        break;
                }

                if (field.Name.Equals("email", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(val))
                {
                    if (!val.Contains('@') || !val.Contains('.'))
                    {
                        errors.Add(new ContractValidationError(rowNumber, field.Name, $"Field '{field.Name}' value '{val}' is not a valid email address."));
                    }
                }
            }
        }

        return errors;
    }

    public static List<ContractValidationError> ValidateCsvFile(string csvPath, string contractJsonPath)
    {
        var content = File.ReadAllText(csvPath);
        return ValidateCsvContent(content, contractJsonPath);
    }
}


