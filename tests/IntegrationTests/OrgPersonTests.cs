using System.Net.Http.Json;
using Core.Data;
using Core.Import;
using Core.Org;
using IntegrationTests.Isolation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MockData;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests;

public class OrgPersonTests : IsolationTestBase
{
    [Fact]
    [Trait("Category", "")]
    public async Task ImportDvtScenario_TransformsSuccessfully_TreeHierarchyAndMemberCountsMatchFixtures()
    {
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var orgService = sp.GetRequiredService<IOrgTransformService>();
            var dvtData = DvtScenarioGenerator.Generate(new ScenarioOptions());

            var batchId = Guid.NewGuid();

            // 寫入暫存區部門
            foreach (var u in dvtData.OrgUnits)
            {
                await dbContext.StgOrgUnits.AddAsync(new StgOrgUnit(
                    Guid.NewGuid(),
                    u.unit_code,
                    u.unit_name,
                    u.parent_code,
                    u.team,
                    u.manager_person_code,
                    TenantAId,
                    batchId));
            }

            // 寫入暫存區人員
            foreach (var p in dvtData.Persons)
            {
                await dbContext.StgPersons.AddAsync(new StgPerson(
                    Guid.NewGuid(),
                    p.person_code,
                    p.display_name,
                    p.unit_code,
                    TenantAId,
                    batchId)
                {
                    Email = p.email,
                    Title = p.title
                });
            }

            await dbContext.SaveChangesAsync();

            // 執行轉換
            var result = await orgService.TransformAsync(batchId);

            Assert.Equal(8, result.OrgUnitsAdded);
            Assert.Equal(0, result.OrgUnitsUpdated);
            Assert.Equal(30, result.PersonsAdded);
            Assert.Equal(0, result.PersonsUpdated);
            Assert.Empty(result.Errors);

            // 驗證正式區實體
            var prodUnits = await dbContext.OrgUnits.ToListAsync();
            Assert.Equal(8, prodUnits.Count);

            var prodPersons = await dbContext.Persons.ToListAsync();
            Assert.Equal(30, prodPersons.Count);

            // 驗證部門樹查詢
            var tree = await orgService.GetOrgTreeAsync();
            Assert.NotEmpty(tree);

            // 計算所有節點的人員總數（遞迴統計）
            int CountMembers(List<OrgTreeNodeDto> nodes) =>
                nodes.Sum(n => n.MemberCount + CountMembers(n.Children));

            Assert.Equal(30, CountMembers(tree));

            // 驗證特定關鍵部門人數與主管（對照 tests/fixtures & DvtScenarioGenerator）
            var allNodes = FlattenTree(tree);
            var pmNode = allNodes.FirstOrDefault(n => n.UnitCode == "PM");
            Assert.NotNull(pmNode);
            Assert.Equal(4, pmNode.MemberCount);
            Assert.Equal("E1001", pmNode.ManagerPersonCode);

            var eeNode = allNodes.FirstOrDefault(n => n.UnitCode == "EE");
            Assert.NotNull(eeNode);
            Assert.Equal(4, eeNode.MemberCount);
            Assert.Equal("E1002", eeNode.ManagerPersonCode);

            var pqeNode = allNodes.FirstOrDefault(n => n.UnitCode == "PQE");
            Assert.NotNull(pqeNode);
            Assert.Equal(3, pqeNode.MemberCount);
            Assert.Equal("E1007", pqeNode.ManagerPersonCode);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task DuplicatePersonImport_WithUpdatedDisplayName_UpdatesExistingRecordWithoutDuplication()
    {
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var orgService = sp.GetRequiredService<IOrgTransformService>();

            // 準備基礎部門
            await dbContext.StgOrgUnits.AddAsync(new StgOrgUnit(
                Guid.NewGuid(), "EE", "電子設計", null, "硬體", null, TenantAId));
            await dbContext.SaveChangesAsync();

            // 第一次匯入人員
            var p1 = new StgPerson(Guid.NewGuid(), "DEMO023", "Example Person", "EE", TenantAId)
            {
                Email = "person@example.com",
                Title = "硬體工程師"
            };
            await dbContext.StgPersons.AddAsync(p1);
            await dbContext.SaveChangesAsync();

            var r1 = await orgService.TransformAsync();
            Assert.Equal(1, r1.PersonsAdded);
            Assert.Equal(0, r1.PersonsUpdated);

            var prod1 = await dbContext.Persons.FirstOrDefaultAsync(p => p.PersonCode == "DEMO023");
            Assert.NotNull(prod1);
            Assert.Equal("Example Person", prod1.DisplayName);
            Assert.Equal("硬體工程師", prod1.Title);

            // 第二次匯入同一個人（名稱與職稱變更）
            var p2 = new StgPerson(Guid.NewGuid(), "DEMO023", "王大明", "EE", TenantAId)
            {
                Email = "daperson@example.com",
                Title = "資深硬體工程師"
            };
            await dbContext.StgPersons.AddAsync(p2);
            await dbContext.SaveChangesAsync();

            var r2 = await orgService.TransformAsync();
            Assert.Equal(0, r2.PersonsAdded);
            Assert.Equal(1, r2.PersonsUpdated);

            // 驗證正式區只有 1 筆，且欄位已更新（使用 AsNoTracking 確保讀取資料庫最新狀態）
            var allPersons = await dbContext.Persons.AsNoTracking().Where(p => p.PersonCode == "DEMO023").ToListAsync();
            Assert.Single(allPersons);
            Assert.Equal("王大明", allPersons[0].DisplayName);
            Assert.Equal("資深硬體工程師", allPersons[0].Title);
            Assert.Equal("daperson@example.com", allPersons[0].Email);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task PersonImport_WithNonExistentUnitCode_GeneratesErrorReportAndDoesNotWriteToProduction()
    {
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var orgService = sp.GetRequiredService<IOrgTransformService>();

            // 匯入一個人，其 unit_code 不存在於任何部門
            var invalidPerson = new StgPerson(Guid.NewGuid(), "E9999", "幽靈員工", "INVALID_DEPT", TenantAId);
            await dbContext.StgPersons.AddAsync(invalidPerson);
            await dbContext.SaveChangesAsync();

            var result = await orgService.TransformAsync();

            Assert.Equal(0, result.PersonsAdded);
            Assert.NotEmpty(result.Errors);

            var error = result.Errors.FirstOrDefault(e => e.FieldName == "unit_code");
            Assert.NotNull(error);
            Assert.Equal("INVALID_DEPT", error.OriginalValue);
            Assert.Contains("不存在", error.Reason);

            // 正式區絕對不能存在該人員
            var prodPerson = await dbContext.Persons.FirstOrDefaultAsync(p => p.PersonCode == "E9999");
            Assert.Null(prodPerson);

            // 錯誤清單已寫入 DB
            var dbError = await dbContext.ImportErrors.FirstOrDefaultAsync(e => e.FieldName == "unit_code" && e.OriginalValue == "INVALID_DEPT");
            Assert.NotNull(dbError);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task GetOrgTreeApi_WithoutTenantContext_ReturnsEmptyList()
    {
        var client = Factory.CreateClient();
        // 不提供任何租戶資訊
        var response = await client.GetAsync("/api/org/tree");
        response.EnsureSuccessStatusCode();

        var nodes = await response.Content.ReadFromJsonAsync<List<OrgTreeNodeDto>>();
        Assert.NotNull(nodes);
        Assert.Empty(nodes);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task TenantIsolation_CompanyACannotAccessCompanyBOrgTree()
    {
        // 租戶 A 匯入資料
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var orgService = sp.GetRequiredService<IOrgTransformService>();
            await dbContext.StgOrgUnits.AddAsync(new StgOrgUnit(Guid.NewGuid(), "HR_A", "人力資源 A", null, null, null, TenantAId));
            await dbContext.StgPersons.AddAsync(new StgPerson(Guid.NewGuid(), "EA01", "員工 A", "HR_A", TenantAId));
            await dbContext.SaveChangesAsync();
            await orgService.TransformAsync();
        });

        // 租戶 B 查詢部門樹，應該完全看不到租戶 A 的部門
        await ExecuteScopedAsync(TenantBId, async (dbContext, sp) =>
        {
            var orgService = sp.GetRequiredService<IOrgTransformService>();
            var tree = await orgService.GetOrgTreeAsync();
            var allNodes = FlattenTree(tree);
            Assert.DoesNotContain(allNodes, n => n.UnitCode == "HR_A");
        });
    }

    private static List<OrgTreeNodeDto> FlattenTree(List<OrgTreeNodeDto> nodes)
    {
        var list = new List<OrgTreeNodeDto>();
        foreach (var node in nodes)
        {
            list.Add(node);
            if (node.Children.Count > 0)
            {
                list.AddRange(FlattenTree(node.Children));
            }
        }
        return list;
    }
}


