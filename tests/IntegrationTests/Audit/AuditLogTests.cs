using Core.Audit;
using Core.Settings;
using IntegrationTests.Isolation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Audit;

public class AuditLogTests : IsolationTestBase
{
    [Fact]
    [Trait("Category", "")]
    public async Task UpdateSetting_GeneratesAuditLog_ContainsOldValueNewValueOperatorAndTimestamp()
    {
        // 驗收條件：改一個設定後，稽核紀錄有舊值、新值、操作人、時間
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var settingService = sp.GetRequiredService<ISettingCenterService>();
            var auditLogService = sp.GetRequiredService<IAuditLogService>();

            // 1. 取得原始設定預設值
            var originalValue = await settingService.GetValueAsync(SettingDefinitions.Keys.CompanyTimeZone);
            Assert.Equal("Asia/Taipei", originalValue);

            // 2. 修改設定值
            var newValue = "America/New_York";
            await settingService.SetValueAsync(SettingDefinitions.Keys.CompanyTimeZone, newValue);

            // 3. 查詢稽核紀錄
            var logs = await auditLogService.QueryAsync(target: SettingDefinitions.Keys.CompanyTimeZone);

            Assert.NotEmpty(logs);
            var log = logs.First();

            Assert.Equal("公司基本", log.Category);
            Assert.Equal("UpdateSetting", log.Action);
            Assert.Equal(SettingDefinitions.Keys.CompanyTimeZone, log.Target);
            Assert.Equal("Asia/Taipei", log.OldValue);
            Assert.Equal("America/New_York", log.NewValue);
            Assert.False(string.IsNullOrWhiteSpace(log.OperatorId));
            Assert.False(string.IsNullOrWhiteSpace(log.OperatorName));
            Assert.True(log.Timestamp > DateTime.UtcNow.AddMinutes(-5));
            Assert.Equal(TenantAId, log.TenantId);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task TenantIsolation_TenantCannotSeeOtherTenantAuditLogs()
    {
        // 驗收條件：A 公司看不到 B 公司的稽核紀錄
        // 1. TenantA 修改設定，留下稽核紀錄
        await ExecuteScopedAsync(TenantAId, async (dbContextA, spA) =>
        {
            var settingServiceA = spA.GetRequiredService<ISettingCenterService>();
            await settingServiceA.SetValueAsync(SettingDefinitions.Keys.CompanyName, "公司A專屬新名稱");
        });

        // 2. TenantB 查詢稽核紀錄，驗證完全看不到 TenantA 的紀錄
        await ExecuteScopedAsync(TenantBId, async (dbContextB, spB) =>
        {
            var auditLogServiceB = spB.GetRequiredService<IAuditLogService>();
            var logsB = await auditLogServiceB.QueryAsync(target: SettingDefinitions.Keys.CompanyName);

            // TenantB 完全查不到 TenantA 針對 CompanyName 的稽核紀錄
            Assert.Empty(logsB);

            // TenantB 查詢全量紀錄，裡面每一筆 TenantId 都必須不是 TenantAId
            var allLogsB = await auditLogServiceB.QueryAsync();
            Assert.DoesNotContain(allLogsB, l => l.TenantId == TenantAId);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task DirectAuditRecord_PersistsAndQueriesCorrectly()
    {
        // 驗證手動/特定模組調用 RecordAsync
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var auditLogService = sp.GetRequiredService<IAuditLogService>();

            await auditLogService.RecordAsync(
                category: "權限變更",
                action: "GrantRole",
                target: "Role.PM",
                oldValue: "None",
                newValue: "FullAccess",
                operatorId: "USR-001",
                operatorName: "平台管理員",
                details: "授權 PM 角色權限");

            var logs = await auditLogService.QueryAsync(category: "權限變更");

            Assert.Single(logs);
            var log = logs[0];
            Assert.Equal("權限變更", log.Category);
            Assert.Equal("GrantRole", log.Action);
            Assert.Equal("Role.PM", log.Target);
            Assert.Equal("None", log.OldValue);
            Assert.Equal("FullAccess", log.NewValue);
            Assert.Equal("USR-001", log.OperatorId);
            Assert.Equal("平台管理員", log.OperatorName);
            Assert.Equal("授權 PM 角色權限", log.Details);
        });
    }
}


