using Core.Import;
using Core.Security;
using IntegrationTests.Isolation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace IntegrationTests.Import;

public class BatchImportApiTests : IsolationTestBase
{
    [Fact]
    [Trait("Category", "")]
    public async Task SameBatchId_SentTwice_ReturnsFirstResultWithoutDuplicateWrite()
    {
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var importService = sp.GetRequiredService<IBatchImportService>();

            var batchId = Guid.NewGuid();
            var jsonRow = JsonSerializer.Deserialize<JsonElement>("""
                {
                    "unit_code": "RD_S205",
                    "unit_name": "核心研發處",
                    "team": "軟體"
                }
            """);

            var payload = new BatchImportPayload
            {
                BatchId = batchId,
                Rows = new List<JsonElement> { jsonRow }
            };

            // 第一次送出
            var r1 = await importService.ImportBatchAsync("stg_org_unit", payload, TenantAId);
            Assert.Equal(batchId, r1.BatchId);
            Assert.Equal(1, r1.TotalRows);
            Assert.Equal(1, r1.SuccessRows);
            Assert.Equal(0, r1.FailedRows);
            Assert.False(r1.IsDuplicate);

            var dbUnitsCount = await dbContext.StgOrgUnits
                .Where(u => u.ImportBatchId == batchId)
                .CountAsync();
            Assert.Equal(1, dbUnitsCount);

            // 第二次送出相同 batchId
            var r2 = await importService.ImportBatchAsync("stg_org_unit", payload, TenantAId);
            Assert.Equal(batchId, r2.BatchId);
            Assert.Equal(1, r2.TotalRows);
            Assert.Equal(1, r2.SuccessRows);
            Assert.True(r2.IsDuplicate); // 標記為重複批次

            // 驗證資料庫依然只有 1 筆，絕無重複寫入
            var dbUnitsCountAfter = await dbContext.StgOrgUnits
                .AsNoTracking()
                .Where(u => u.ImportBatchId == batchId)
                .CountAsync();
            Assert.Equal(1, dbUnitsCountAfter);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task DisabledApiKey_ReturnsNullAndIsRejected()
    {
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var importService = sp.GetRequiredService<IBatchImportService>();

            var activeKey = "key_active_" + Guid.NewGuid().ToString("N");
            var disabledKey = "key_disabled_" + Guid.NewGuid().ToString("N");

            await dbContext.TenantApiKeys.AddAsync(new TenantApiKey(
                Guid.NewGuid(), activeKey, "有效金鑰", TenantAId, isActive: true));
            await dbContext.TenantApiKeys.AddAsync(new TenantApiKey(
                Guid.NewGuid(), disabledKey, "已停用金鑰", TenantAId, isActive: false));
            await dbContext.SaveChangesAsync();

            // 有效金鑰驗證成功並回傳 TenantId
            var authActive = await importService.AuthenticateApiKeyAsync(activeKey);
            Assert.Equal(TenantAId, authActive);

            // 停用的金鑰回傳 null (API 端點對應 401 Unauthorized)
            var authDisabled = await importService.AuthenticateApiKeyAsync(disabledKey);
            Assert.Null(authDisabled);

            // 不存在的金鑰回傳 null
            var authNonExistent = await importService.AuthenticateApiKeyAsync("non_existent_key");
            Assert.Null(authNonExistent);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task TenantA_ApiKey_CannotWriteToTenantB()
    {
        var keyA = "key_company_a_" + Guid.NewGuid().ToString("N");
        var batchIdA = Guid.NewGuid();

        // 1. 為 TenantA 配置專屬金鑰並匯入人員資料
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var importService = sp.GetRequiredService<IBatchImportService>();

            await dbContext.TenantApiKeys.AddAsync(new TenantApiKey(
                Guid.NewGuid(), keyA, "A公司專用金鑰", TenantAId, isActive: true));
            await dbContext.SaveChangesAsync();

            // 驗證金鑰取得租戶
            var authTenant = await importService.AuthenticateApiKeyAsync(keyA);
            Assert.Equal(TenantAId, authTenant);

            var personRow = JsonSerializer.Deserialize<JsonElement>("""
                {
                    "person_code": "E8888",
                    "display_name": "林專員",
                    "unit_code": "EE",
                    "email": "lin@example.com"
                }
            """);

            var payload = new BatchImportPayload
            {
                BatchId = batchIdA,
                Rows = new List<JsonElement> { personRow }
            };

            var res = await importService.ImportBatchAsync("stg_person", payload, authTenant);
            Assert.Equal(1, res.SuccessRows);
        });

        // 2. TenantB 查詢資料庫：TenantA 匯入的人員與批次在 TenantB 完全查不到
        await ExecuteScopedAsync(TenantBId, async (dbContext, sp) =>
        {
            var personInB = await dbContext.StgPersons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonCode == "E8888");
            Assert.Null(personInB);

            var batchInB = await dbContext.ImportBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchIdA);
            Assert.Null(batchInB);
        });
    }
}



