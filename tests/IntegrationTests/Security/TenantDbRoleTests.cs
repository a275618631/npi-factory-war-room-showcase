using Core.Data;
using Core.Security;
using Core.Settings;
using IntegrationTests.Isolation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text.Json;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests.Security;

public class TenantDbRoleTests : IsolationTestBase
{
    private const string TenantACode = "CorpA";
    private const string TenantBCode = "CorpB";
    private static readonly string ImporterPassword = Guid.NewGuid().ToString("N");

    [Fact]
    [Trait("Category", "")]
    public async Task CreateTenantImporterRole_WhenDirectDbAccessDisabled_DoesNotCreateRole()
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var settingCenter = scope.ServiceProvider.GetRequiredService<ISettingCenterService>();
        var dbRoleService = scope.ServiceProvider.GetRequiredService<ITenantDbRoleService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();

        using (currentTenant.Change(TenantAId))
        {
            // 確保設定關閉（預設即為 false）
            await settingCenter.SetValueAsync(SettingDefinitions.Keys.DirectDbAccess, "false");

            var result = await dbRoleService.CreateTenantImporterRoleAsync(TenantAId, TenantACode, ImporterPassword);

            // 斷言回傳失敗
            Assert.False(result.Success);
            Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);

            // 斷言 PostgreSQL 中未建立該角色
            var roleName = dbRoleService.GetRoleName(TenantACode);
            var roleExists = await CheckRoleExistsAsync(dbContext, roleName);
            Assert.False(roleExists, $"Role {roleName} should not exist in pg_roles when disabled.");
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task CreateTenantImporterRole_WhenEnabled_AllowsInsertOwnStagingTable_DeniesReadAndCrossTenant()
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var settingCenter = scope.ServiceProvider.GetRequiredService<ISettingCenterService>();
        var dbRoleService = scope.ServiceProvider.GetRequiredService<ITenantDbRoleService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();

        string roleName;
        using (currentTenant.Change(TenantAId))
        {
            // 1. 開啟設定
            await settingCenter.SetValueAsync(SettingDefinitions.Keys.DirectDbAccess, "true");

            // 2. 建立 Tenant A 的匯入角色
            var result = await dbRoleService.CreateTenantImporterRoleAsync(TenantAId, TenantACode, ImporterPassword);
            Assert.True(result.Success);

            roleName = dbRoleService.GetRoleName(TenantACode);
            var roleExists = await CheckRoleExistsAsync(dbContext, roleName);
            Assert.True(roleExists, $"Role {roleName} must exist in pg_roles.");
        }

        // 3. 建立以該 importer 角色登入的資料庫連線
        var importerConnBuilder = new NpgsqlConnectionStringBuilder(DbContainer.GetConnectionString())
        {
            Username = roleName,
            Password = ImporterPassword
        };

        await using var importerConn = new NpgsqlConnection(importerConnBuilder.ConnectionString);
        await importerConn.OpenAsync();

        // 驗收條件 A：不能讀正式區（如 person、projects）
        await using (var cmd = importerConn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM person;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteScalarAsync());
            Assert.Equal("42501", ex.SqlState); // 42501 = insufficient_privilege
        }

        await using (var cmd = importerConn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM projects;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteScalarAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 驗收條件 B：不能讀取匯入表（stg_person），僅限 INSERT
        await using (var cmd = importerConn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM stg_person;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteScalarAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 驗收條件 C：只能 INSERT 自己公司的匯入表
        var personAId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var cmd = importerConn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO stg_person ("Id", "PersonCode", "DisplayName", "UnitCode", "TenantId", "ImportBatchId", "CreatedAt")
                VALUES (@id, @personCode, @name, @unitCode, @tenantId, @batchId, @createdAt);
                """;
            cmd.Parameters.AddWithValue("id", personAId);
            cmd.Parameters.AddWithValue("personCode", "P-A001");
            cmd.Parameters.AddWithValue("name", "Alice Chen");
            cmd.Parameters.AddWithValue("unitCode", "ENG");
            cmd.Parameters.AddWithValue("tenantId", TenantAId);
            cmd.Parameters.AddWithValue("batchId", batchId);
            cmd.Parameters.AddWithValue("createdAt", now);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            Assert.Equal(1, rowsAffected);
        }

        // 驗收條件 D：嘗試插入非自己公司（Tenant B）的資料，應被 RLS 擋下
        var personBId = Guid.NewGuid();
        await using (var cmd = importerConn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO stg_person ("Id", "PersonCode", "DisplayName", "UnitCode", "TenantId", "ImportBatchId", "CreatedAt")
                VALUES (@id, @personCode, @name, @unitCode, @tenantId, @batchId, @createdAt);
                """;
            cmd.Parameters.AddWithValue("id", personBId);
            cmd.Parameters.AddWithValue("personCode", "P-B001");
            cmd.Parameters.AddWithValue("name", "Bob Wang");
            cmd.Parameters.AddWithValue("unitCode", "ENG");
            cmd.Parameters.AddWithValue("tenantId", TenantBId); // 試圖冒用 Tenant B
            cmd.Parameters.AddWithValue("batchId", batchId);
            cmd.Parameters.AddWithValue("createdAt", now);

            var rlsEx = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", rlsEx.SqlState); // 42501 = insufficient_privilege (RLS policy check failed)
            Assert.Contains("row-level security", rlsEx.MessageText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "")]
    public void NodeRedTemplateAndDocumentation_AreValidAndComplete()
    {
        // 1. 驗證 import-flow.json 格式與節點完整性
        var repoRoot = FindRepoRoot();
        var flowJsonPath = Path.Combine(repoRoot, "integrations", "node-red", "import-flow.json");
        Assert.True(File.Exists(flowJsonPath), $"Flow JSON file not found at {flowJsonPath}");

        var jsonContent = File.ReadAllText(flowJsonPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        Assert.Equal(JsonValueKind.Array, jsonDoc.RootElement.ValueKind);

        var nodeTypes = jsonDoc.RootElement.EnumerateArray()
            .Select(n => n.GetProperty("type").GetString())
            .ToList();

        Assert.Contains("tab", nodeTypes);
        Assert.Contains("inject", nodeTypes);
        Assert.Contains("file in", nodeTypes);
        Assert.Contains("csv", nodeTypes);
        Assert.Contains("function", nodeTypes);
        Assert.Contains("http request", nodeTypes);
        Assert.Contains("debug", nodeTypes);

        // 驗證 function 節點包含關鍵邏輯
        Assert.Contains("batchId", jsonContent);
        Assert.Contains("X-Api-Key", jsonContent);
        Assert.Contains("/api/import/", jsonContent);

        // 2. 驗證 README.md 步驟完整
        var readmePath = Path.Combine(repoRoot, "integrations", "node-red", "README.md");
        Assert.True(File.Exists(readmePath), $"README file not found at {readmePath}");

        var readmeContent = File.ReadAllText(readmePath);
        Assert.True(readmeContent.Length > 300);
        Assert.Contains("Node-RED", readmeContent);
        Assert.Contains("Docker", readmeContent);
        Assert.Contains("API_KEY", readmeContent);
        Assert.Contains("import-flow.json", readmeContent);
    }

    private static async Task<bool> CheckRoleExistsAsync(NpiDbContext dbContext, string roleName)
    {
        await using var cmd = dbContext.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pg_roles WHERE rolname = @rolname;";
        var p = cmd.CreateParameter();
        p.ParameterName = "rolname";
        p.Value = roleName;
        cmd.Parameters.Add(p);

        await dbContext.Database.OpenConnectionAsync();
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "NpiPlatform.sln")) ||
                Directory.Exists(Path.Combine(current, "contracts")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}




