using Core.Data;
using Core.Projects;
using Core.Settings;
using IntegrationTests.Isolation;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests.Routing;

public class DeepLinkRoutingTests : IsolationTestBase
{
    private async Task SeedSampleProjectAndOrgAsync(Guid tenantId)
    {
        var batchId = Guid.NewGuid();

        await ExecuteScopedAsync(tenantId, async (dbContext, sp) =>
        {
            var transformService = sp.GetRequiredService<IProjectTransformService>();

            // 1. 建立專案
            var stgProject = new StgProject(
                Guid.NewGuid(),
                projectCode: "NPI-2601",
                projectName: "範例產品 A",
                pmPersonCode: "E1001",
                currentStageCode: "DVT",
                tenantId: tenantId,
                importBatchId: batchId);
            await dbContext.StgProjects.AddAsync(stgProject);

            // 2. 建立階段計畫
            var stages = new[]
            {
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "Concept", new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 15), tenantId: tenantId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "Feasibility", new DateOnly(2026, 5, 16), new DateOnly(2026, 7, 15), tenantId: tenantId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "EVT", new DateOnly(2026, 7, 21), new DateOnly(2026, 9, 30), tenantId: tenantId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "DVT", new DateOnly(2026, 10, 6), new DateOnly(2026, 12, 15), tenantId: tenantId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "PVT", new DateOnly(2026, 12, 16), new DateOnly(2027, 2, 15), tenantId: tenantId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "MP", new DateOnly(2027, 2, 16), new DateOnly(2027, 4, 30), tenantId: tenantId, importBatchId: batchId)
            };
            await dbContext.StgStagePlans.AddRangeAsync(stages);

            // 3. 建立單位
            var orgUnit = new Core.Org.OrgUnit(
                Guid.NewGuid(),
                unitCode: "SQE",
                unitName: "供應商品質",
                tenantId: tenantId);
            await dbContext.OrgUnits.AddAsync(orgUnit);

            await dbContext.SaveChangesAsync();
            await transformService.TransformAsync(batchId);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task DeepLinkContext_FromOverviewToDeliverable_GeneratesCorrectFiveLevelBreadcrumb()
    {
        await SeedSampleProjectAndOrgAsync(TenantAId);

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("__tenant", TenantAId.ToString());

        var url = "/api/deep-links/context?projectCode=NPI-2601&stageCode=DVT&unitCode=SQE&itemType=deliverable&itemCode=DVT-PPAP";
        var res = await client.GetAsync(url);
        res.EnsureSuccessStatusCode();

        var data = await res.Content.ReadFromJsonAsync<DeepLinkContextResponse>();
        Assert.NotNull(data);
        Assert.True(data.HasPermission);
        Assert.Equal(5, data.Breadcrumb.Count);

        // L0: 全部專案
        Assert.Equal("L0", data.Breadcrumb[0].Level);
        Assert.Equal("全部專案", data.Breadcrumb[0].Name);
        Assert.Equal("/overview", data.Breadcrumb[0].Path);

        // L1: 專案代號和名稱
        Assert.Equal("L1", data.Breadcrumb[1].Level);
        Assert.Equal("NPI-2601 範例產品 A", data.Breadcrumb[1].Name);
        Assert.Equal("/p/NPI-2601", data.Breadcrumb[1].Path);

        // L2: 關卡名稱（讀設定，預設「設計驗證」）
        Assert.Equal("L2", data.Breadcrumb[2].Level);
        Assert.Equal("設計驗證", data.Breadcrumb[2].Name);
        Assert.Equal("/p/NPI-2601/g/DVT", data.Breadcrumb[2].Path);

        // L3: 單位名稱
        Assert.Equal("L3", data.Breadcrumb[3].Level);
        Assert.Equal("SQE 供應商品質", data.Breadcrumb[3].Name);
        Assert.Equal("/p/NPI-2601/g/DVT/u/SQE", data.Breadcrumb[3].Path);

        // L4: 交付物代號和名稱
        Assert.Equal("L4", data.Breadcrumb[4].Level);
        Assert.Equal("DVT-PPAP PPAP 文件", data.Breadcrumb[4].Name);
        Assert.Equal("/p/NPI-2601/item/deliverable/DVT-PPAP", data.Breadcrumb[4].Path);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task DeepLinkContext_OpenL4Directly_FillsCompleteBreadcrumbPath()
    {
        await SeedSampleProjectAndOrgAsync(TenantAId);

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("__tenant", TenantAId.ToString());

        // 直接打開 L4，只傳 projectCode、itemType、itemCode
        var url = "/api/deep-links/context?projectCode=NPI-2601&itemType=deliverable&itemCode=DVT-PPAP";
        var res = await client.GetAsync(url);
        res.EnsureSuccessStatusCode();

        var data = await res.Content.ReadFromJsonAsync<DeepLinkContextResponse>();
        Assert.NotNull(data);
        Assert.True(data.HasPermission);

        // 驗收條件 2：直接打開 L4 的網址，麵包屑一樣完整包含 L0~L4
        Assert.Equal(5, data.Breadcrumb.Count);
        Assert.Equal("全部專案", data.Breadcrumb[0].Name);
        Assert.Equal("NPI-2601 範例產品 A", data.Breadcrumb[1].Name);
        Assert.Equal("設計驗證", data.Breadcrumb[2].Name);
        Assert.Equal("SQE 供應商品質", data.Breadcrumb[3].Name);
        Assert.Equal("DVT-PPAP PPAP 文件", data.Breadcrumb[4].Name);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task DeepLinkContext_AfterStageRenamed_BreadcrumbShowsNewName()
    {
        await SeedSampleProjectAndOrgAsync(TenantAId);

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("__tenant", TenantAId.ToString());

        // 1. 將 EVT 改名為「工程樣品驗證」
        var getStagesRes = await client.GetAsync("/api/settings/stages");
        getStagesRes.EnsureSuccessStatusCode();
        var stages = await getStagesRes.Content.ReadFromJsonAsync<List<StageConfigItem>>();
        Assert.NotNull(stages);

        var target = stages.First(s => s.Code == "EVT");
        target.Name = "工程樣品驗證";

        var putRes = await client.PutAsJsonAsync("/api/settings/stages", stages);
        putRes.EnsureSuccessStatusCode();

        // 2. 查詢 EVT 關卡麵包屑
        var url = "/api/deep-links/context?projectCode=NPI-2601&stageCode=EVT";
        var res = await client.GetAsync(url);
        res.EnsureSuccessStatusCode();

        var data = await res.Content.ReadFromJsonAsync<DeepLinkContextResponse>();
        Assert.NotNull(data);

        // 驗收條件 3：階段改名後，麵包屑顯示新名稱
        var l2 = data.Breadcrumb.FirstOrDefault(b => b.Level == "L2");
        Assert.NotNull(l2);
        Assert.Equal("工程樣品驗證", l2.Name);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task DeepLinkContext_TenantB_AccessingTenantAProject_ReturnsForbiddenAndHidesAllDetails()
    {
        await SeedSampleProjectAndOrgAsync(TenantAId);

        // Tenant B 使用者嘗試存取 Tenant A 的專案 NPI-2601
        var clientB = Factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("__tenant", TenantBId.ToString());

        var url = "/api/deep-links/context?projectCode=NPI-2601&stageCode=DVT&unitCode=SQE&itemType=deliverable&itemCode=DVT-PPAP";
        var res = await clientB.GetAsync(url);

        // 驗收條件 4：B 公司的使用者打開 A 公司專案的網址，看到「沒有權限」，畫面和回應裡都沒有 A 公司的任何名稱
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var responseBody = await res.Content.ReadAsStringAsync();
        Assert.Contains("沒有權限", responseBody);
        Assert.DoesNotContain("範例產品 A", responseBody);
        Assert.DoesNotContain("供應商品質", responseBody);
        Assert.DoesNotContain("PPAP 文件", responseBody);
    }
}


