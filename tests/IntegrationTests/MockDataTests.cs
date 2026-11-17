using System.Security.Cryptography;
using Core.Data;
using Core.Import;
using IntegrationTests.Isolation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MockData;
using Xunit;

namespace IntegrationTests;

public class MockDataTests : IsolationTestBase
{
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
    public void DeterministicSeed_ProducesBitExactIdenticalFiles()
    {
        var options = new ScenarioOptions { Seed = 20261103 };
        var tempDir1 = Path.Combine(Path.GetTempPath(), "mock_test_seed_1_" + Guid.NewGuid().ToString("N"));
        var tempDir2 = Path.Combine(Path.GetTempPath(), "mock_test_seed_2_" + Guid.NewGuid().ToString("N"));

        try
        {
            var ds1 = DvtScenarioGenerator.Generate(options);
            DvtScenarioGenerator.WriteCsvFiles(ds1, tempDir1);

            var ds2 = DvtScenarioGenerator.Generate(options);
            DvtScenarioGenerator.WriteCsvFiles(ds2, tempDir2);

            var fileNames = new[]
            {
                "stg_org_unit.csv",
                "stg_person.csv",
                "stg_project.csv",
                "stg_stage_plan.csv",
                "stg_deliverable.csv",
                "stg_blocker.csv"
            };

            foreach (var fn in fileNames)
            {
                var file1 = Path.Combine(tempDir1, fn);
                var file2 = Path.Combine(tempDir2, fn);

                Assert.True(File.Exists(file1), $"File {file1} not created");
                Assert.True(File.Exists(file2), $"File {file2} not created");

                var hash1 = SHA256.HashData(File.ReadAllBytes(file1));
                var hash2 = SHA256.HashData(File.ReadAllBytes(file2));

                Assert.Equal(Convert.ToHexString(hash1), Convert.ToHexString(hash2));
            }
        }
        finally
        {
            if (Directory.Exists(tempDir1)) Directory.Delete(tempDir1, true);
            if (Directory.Exists(tempDir2)) Directory.Delete(tempDir2, true);
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
    public void Contracts_AllGeneratedCsvsPassContractValidation(string tableName)
    {
        var repoRoot = FindRepoRoot();
        var contractPath = Path.Combine(repoRoot, "contracts", "v1", $"{tableName}.json");
        Assert.True(File.Exists(contractPath), $"Contract not found: {contractPath}");

        var options = new ScenarioOptions { Seed = 20261103 };
        var tempDir = Path.Combine(Path.GetTempPath(), "mock_test_contract_" + Guid.NewGuid().ToString("N"));

        try
        {
            var dataSet = DvtScenarioGenerator.Generate(options);
            DvtScenarioGenerator.WriteCsvFiles(dataSet, tempDir);

            var csvPath = Path.Combine(tempDir, $"{tableName}.csv");
            Assert.True(File.Exists(csvPath), $"CSV not found: {csvPath}");

            var errors = ContractValidator.ValidateCsvFile(csvPath, contractPath);
            Assert.Empty(errors);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "")]
    public void ScenarioCounts_MatchScenarioOptions()
    {
        var options = new ScenarioOptions();
        var dataSet = DvtScenarioGenerator.Generate(options);

        // Department count
        Assert.Equal(options.DepartmentCount, dataSet.OrgUnits.Count);

        // Person count
        Assert.Equal(options.PersonCount, dataSet.Persons.Count);

        // Project count
        Assert.Equal(options.ProjectCount, dataSet.Projects.Count);

        // Stage count
        Assert.Equal(options.StageCount, dataSet.StagePlans.Count);

        // Deliverable count and overdue
        Assert.Equal(options.DeliverableCount, dataSet.Deliverables.Count);
        var overdueDeliverables = dataSet.Deliverables
            .Where(d => d.due_date < options.BaseDate && d.status != "已核准" && d.status != "已交")
            .ToList();
        Assert.Equal(options.OverdueDeliverableCount, overdueDeliverables.Count);

        // Blocker count and > 3 days overdue
        Assert.Equal(options.BlockerCount, dataSet.Blockers.Count);
        var longOverdueBlockers = dataSet.Blockers
            .Where(b => b.status != "已解除" && (options.BaseDate.DayNumber - b.since_date.DayNumber) > 3)
            .ToList();
        Assert.Equal(options.LongOverdueBlockerCount, longOverdueBlockers.Count);
    }

    [Fact]
    [Trait("Category", "")]
    public void DrillDownRequirements_AllSatisfied()
    {
        var options = new ScenarioOptions();
        var dataSet = DvtScenarioGenerator.Generate(options);
        var personCodes = dataSet.Persons.Select(p => p.person_code).ToHashSet();

        // 1. Every department has a unit manager
        Assert.All(dataSet.OrgUnits, u =>
        {
            Assert.False(string.IsNullOrWhiteSpace(u.manager_person_code), $"Department {u.unit_code} has no manager");
            Assert.Contains(u.manager_person_code, personCodes);
        });

        // 2. Every stage has a gate owner
        Assert.All(dataSet.StagePlans, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.gate_owner_person_code), $"Stage {s.stage_code} has no gate owner");
            Assert.Contains(s.gate_owner_person_code, personCodes);
        });

        // 3. 6 blockers distributed across at least 3 departments, 2 stages, 3 reason categories
        var distinctUnits = dataSet.Blockers.Select(b => b.unit_code).Distinct().ToList();
        Assert.True(distinctUnits.Count >= 3, $"Expected at least 3 distinct units in blockers, got {distinctUnits.Count}");

        var distinctStages = dataSet.Blockers.Select(b => b.stage_code).Distinct().ToList();
        Assert.True(distinctStages.Count >= 2, $"Expected at least 2 distinct stages in blockers, got {distinctStages.Count}");

        var distinctReasons = dataSet.Blockers
            .Select(b => b.reason_category)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct()
            .ToList();
        Assert.True(distinctReasons.Count >= 3, $"Expected at least 3 distinct reason categories, got {distinctReasons.Count}");

        // 4. Exactly 1 blocker resolved with resolved date and resolution note
        var resolvedBlockers = dataSet.Blockers.Where(b => b.status == "已解除").ToList();
        Assert.Single(resolvedBlockers);
        Assert.NotNull(resolvedBlockers[0].resolved_date);
        Assert.False(string.IsNullOrWhiteSpace(resolvedBlockers[0].resolution_note));

        // 5. At least 1 blocker escalated
        var escalatedBlockers = dataSet.Blockers.Where(b => !string.IsNullOrWhiteSpace(b.escalated_to_person_code)).ToList();
        Assert.NotEmpty(escalatedBlockers);
        Assert.All(escalatedBlockers, b => Assert.Contains(b.escalated_to_person_code!, personCodes));

        // 6. Every blocker owner is in the personnel list
        Assert.All(dataSet.Blockers, b => Assert.Contains(b.owner_person_code, personCodes));
    }

    [Fact]
    [Trait("Category", "")]
    public async Task ImportFlow_PersonsCsv_ImportsSuccessfullyViaS110()
    {
        var options = new ScenarioOptions();
        var tempDir = Path.Combine(Path.GetTempPath(), "mock_test_s110_" + Guid.NewGuid().ToString("N"));

        try
        {
            var dataSet = DvtScenarioGenerator.Generate(options);
            DvtScenarioGenerator.WriteCsvFiles(dataSet, tempDir);

            var personCsvPath = Path.Combine(tempDir, "stg_person.csv");
            Assert.True(File.Exists(personCsvPath));

            await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
            {
                var importService = sp.GetRequiredService<PersonImportService>();
                var result = await DvtScenarioGenerator.ImportPersonsAsync(importService, personCsvPath);

                Assert.False(result.IsDuplicate);
                Assert.Equal(ImportBatchStatus.Success, result.Batch.Status);
                Assert.Equal(30, result.Batch.TotalRows);
                Assert.Equal(30, result.Batch.SuccessRows);
                Assert.Equal(0, result.Batch.FailedRows);
                Assert.Empty(result.Errors);
                Assert.Null(result.ErrorReportExcel);

                var savedPersons = await dbContext.StgPersons
                    .Where(p => p.ImportBatchId == result.Batch.Id)
                    .ToListAsync();

                Assert.Equal(30, savedPersons.Count);
            });
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}


