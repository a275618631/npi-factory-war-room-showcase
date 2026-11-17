using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace UnitTests;

public class DataContractTests
{
    private static readonly string[] TargetTables =
    {
        "stg_org_unit",
        "stg_person",
        "stg_project",
        "stg_stage_plan",
        "stg_deliverable",
        "stg_blocker"
    };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NpiPlatform.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    [Fact]
    [Trait("Category", "")]
    public void Contracts_AllSixTableJsonFilesExist()
    {
        var repoRoot = FindRepoRoot();
        var contractsDir = Path.Combine(repoRoot, "contracts", "v1");

        Assert.True(Directory.Exists(contractsDir), $"Directory not found: {contractsDir}");

        foreach (var table in TargetTables)
        {
            var filePath = Path.Combine(contractsDir, $"{table}.json");
            Assert.True(File.Exists(filePath), $"Data contract JSON file not found: {filePath}");
        }
    }

    [Theory]
    [InlineData("stg_org_unit")]
    [InlineData("stg_person")]
    [InlineData("stg_project")]
    [InlineData("stg_stage_plan")]
    [InlineData("stg_deliverable")]
    [InlineData("stg_blocker")]
    [Trait("Category", "")]
    public void Contracts_FieldsMatchDataContractDoc(string tableName)
    {
        var repoRoot = FindRepoRoot();
        var docPath = Path.Combine(repoRoot, "docs", "DATA_CONTRACT.md");
        var jsonPath = Path.Combine(repoRoot, "contracts", "v1", $"{tableName}.json");

        Assert.True(File.Exists(docPath), $"DATA_CONTRACT.md not found at {docPath}");
        Assert.True(File.Exists(jsonPath), $"{tableName}.json not found at {jsonPath}");

        var docContent = File.ReadAllText(docPath);
        var docFields = ParseMarkdownTableFields(docContent, tableName);
        Assert.NotEmpty(docFields);

        var contract = LoadContract(jsonPath);
        Assert.Equal(tableName, contract.TableName);
        Assert.Equal("v1", contract.Version);

        var jsonFields = contract.Fields.Select(f => f.Name).ToList();

        var mismatchError = CheckFieldMismatch(tableName, docFields, jsonFields);
        Assert.Null(mismatchError);
    }

    [Fact]
    [Trait("Category", "")]
    public void Contracts_MismatchDetection_FailsAndIdentifiesField()
    {
        var expected = new List<string> { "field_a", "field_b", "field_c" };
        var actualMissing = new List<string> { "field_a", "field_c" };
        var actualExtra = new List<string> { "field_a", "field_b", "field_c", "field_d" };

        var missingErr = CheckFieldMismatch("stg_demo", expected, actualMissing);
        Assert.NotNull(missingErr);
        Assert.Contains("field_b", missingErr);
        Assert.Contains("missing", missingErr);

        var extraErr = CheckFieldMismatch("stg_demo", expected, actualExtra);
        Assert.NotNull(extraErr);
        Assert.Contains("field_d", extraErr);
        Assert.Contains("unexpected", extraErr);
    }

    [Fact]
    [Trait("Category", "")]
    public void Contracts_ViewModeNewFields_AreAllMarkedOptional()
    {
        var repoRoot = FindRepoRoot();
        var contractsDir = Path.Combine(repoRoot, "contracts", "v1");

        // 2026-11-03 fields:
        // - stg_org_unit: manager_person_code
        // - stg_stage_plan: gate_owner_person_code
        // - stg_deliverable: percent_done, unit_code
        // - stg_blocker: reason_category, escalated_to_person_code, resolved_date, resolution_note
        var checks = new (string Table, string FieldName)[]
        {
            ("stg_org_unit", "manager_person_code"),
            ("stg_stage_plan", "gate_owner_person_code"),
            ("stg_deliverable", "percent_done"),
            ("stg_deliverable", "unit_code"),
            ("stg_blocker", "reason_category"),
            ("stg_blocker", "escalated_to_person_code"),
            ("stg_blocker", "resolved_date"),
            ("stg_blocker", "resolution_note")
        };

        foreach (var (table, fieldName) in checks)
        {
            var jsonPath = Path.Combine(contractsDir, $"{table}.json");
            var contract = LoadContract(jsonPath);
            var field = contract.Fields.FirstOrDefault(f => f.Name == fieldName);

            Assert.NotNull(field);
            Assert.False(field.Required, $"Field '{fieldName}' in table '{table}' must be marked as optional (required: false).");
        }
    }

    [Theory]
    [InlineData("stg_org_unit")]
    [InlineData("stg_person")]
    [InlineData("stg_project")]
    [InlineData("stg_stage_plan")]
    [InlineData("stg_deliverable")]
    [InlineData("stg_blocker")]
    [Trait("Category", "")]
    public void Contracts_AllFieldsHaveTypeAndDescription(string tableName)
    {
        var repoRoot = FindRepoRoot();
        var jsonPath = Path.Combine(repoRoot, "contracts", "v1", $"{tableName}.json");
        var contract = LoadContract(jsonPath);

        Assert.NotEmpty(contract.Fields);
        foreach (var field in contract.Fields)
        {
            Assert.False(string.IsNullOrWhiteSpace(field.Name), $"Field in {tableName} has empty name");
            Assert.False(string.IsNullOrWhiteSpace(field.Type), $"Field {field.Name} in {tableName} has empty type");
            Assert.False(string.IsNullOrWhiteSpace(field.Description), $"Field {field.Name} in {tableName} has empty description");
        }
    }

    public static List<string> ParseMarkdownTableFields(string markdownContent, string tableName)
    {
        var lines = markdownContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var inTargetSection = false;
        var inTable = false;
        var fields = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.StartsWith("### ") && line.Contains(tableName))
            {
                inTargetSection = true;
                inTable = false;
                continue;
            }

            if (inTargetSection)
            {
                if (line.StartsWith("### ") || line.StartsWith("## "))
                {
                    // Next section reached
                    break;
                }

                if (line.StartsWith("|"))
                {
                    var columns = line.Split('|', StringSplitOptions.TrimEntries);
                    // Header or separator line
                    if (columns.Length < 3 || columns[1] == "欄位" || columns[1].StartsWith("---"))
                    {
                        inTable = true;
                        continue;
                    }

                    if (inTable)
                    {
                        var fieldCol = columns[1];
                        var subFields = fieldCol.Split('、', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        foreach (var sf in subFields)
                        {
                            if (!string.IsNullOrWhiteSpace(sf))
                            {
                                fields.Add(sf);
                            }
                        }
                    }
                }
                else if (inTable && string.IsNullOrWhiteSpace(line))
                {
                    // Table ended
                    break;
                }
            }
        }

        return fields;
    }

    public static string? CheckFieldMismatch(string tableName, IEnumerable<string> expectedFields, IEnumerable<string> actualFields)
    {
        var expectedSet = expectedFields.ToHashSet();
        var actualSet = actualFields.ToHashSet();

        var missing = expectedSet.Except(actualSet).ToList();
        var unexpected = actualSet.Except(expectedSet).ToList();

        if (missing.Count == 0 && unexpected.Count == 0)
        {
            return null;
        }

        var parts = new List<string>();
        if (missing.Count > 0)
        {
            parts.Add($"missing fields in JSON: [{string.Join(", ", missing)}]");
        }
        if (unexpected.Count > 0)
        {
            parts.Add($"unexpected fields in JSON: [{string.Join(", ", unexpected)}]");
        }

        return $"Table '{tableName}' contract mismatch: {string.Join("; ", parts)}";
    }

    public static ContractDefinition LoadContract(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var contract = JsonSerializer.Deserialize<ContractDefinition>(json, options);
        Assert.NotNull(contract);
        return contract;
    }

    public class ContractDefinition
    {
        [JsonPropertyName("table_name")]
        public string TableName { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("fields")]
        public List<ContractField> Fields { get; set; } = new();
    }

    public class ContractField
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
}


