using Core.Data;
using Core.Projects;
using Core.Settings;
using IntegrationTests.Isolation;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests.Projects;

public class StageTimelineTests : IsolationTestBase
{
    private async Task SeedSampleProjectAsync(Guid tenantId)
    {
        var asOfDate = new DateOnly(2026, 11, 20);
        var batchId = Guid.NewGuid();

        await ExecuteScopedAsync(tenantId, async (dbContext, sp) =>
        {
            var transformService = sp.GetRequiredService<IProjectTransformService>();

            var stgProject = new StgProject(
                Guid.NewGuid(),
                projectCode: "NPI-2601",
                projectName: "範例產品 A",
                pmPersonCode: "E1001",
                currentStageCode: "DVT",
                tenantId: tenantId,
                importBatchId: batchId)
            {
                CurrentBuild = "DVT-2",
                PlannedQty = 600,
                CustomerRef = "客戶 A"
            };
            await dbContext.StgProjects.AddAsync(stgProject);

            var stages = new[]
            {
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "Concept", new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 15),
                    actualStart: new DateOnly(2026, 4, 1), actualEnd: new DateOnly(2026, 5, 15), gateDate: new DateOnly(2026, 5, 18), gateOwnerPersonCode: "E1001", tenantId: tenantId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "Feasibility", new DateOnly(2026, 5, 16), new DateOnly(2026, 7, 15),
                    actualStart: new DateOnly(2026, 5, 16), actualEnd: new DateOnly(2026, 7, 20), gateDate: new DateOnly(2026, 7, 23), gateOwnerPersonCode: "E1002", tenantId: tenantId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "EVT", new DateOnly(2026, 7, 21), new DateOnly(2026, 9, 30),
                    actualStart: new DateOnly(2026, 7, 21), actualEnd: new DateOnly(2026, 10, 5), gateDate: new DateOnly(2026, 10, 8), gateOwnerPersonCode: "E1003", tenantId: tenantId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "DVT", new DateOnly(2026, 10, 6), new DateOnly(2026, 12, 15),
                    actualStart: new DateOnly(2026, 10, 6), actualEnd: null, gateDate: new DateOnly(2026, 12, 18), gateOwnerPersonCode: "E1004", tenantId: tenantId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "PVT", new DateOnly(2026, 12, 16), new DateOnly(2027, 2, 15),
                    actualStart: null, actualEnd: null, gateDate: new DateOnly(2027, 2, 18), gateOwnerPersonCode: "E1005", tenantId: tenantId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "MP", new DateOnly(2027, 2, 16), new DateOnly(2027, 4, 30),
                    actualStart: null, actualEnd: null, gateDate: new DateOnly(2027, 5, 5), gateOwnerPersonCode: "E1001", tenantId: tenantId, importBatchId: batchId)
            };

            await dbContext.StgStagePlans.AddRangeAsync(stages);
            await dbContext.SaveChangesAsync();

            var transformRes = await transformService.TransformAsync(batchId, asOfDate);
            Assert.Empty(transformRes.Errors);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task StageTimeline_ShowsCurrentStageDvtAndDelayedStagesWithDays()
    {
        await SeedSampleProjectAsync(TenantAId);

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("__tenant", TenantAId.ToString());

        var response = await client.GetAsync("/api/projects/NPI-2601");
        response.EnsureSuccessStatusCode();

        var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);
        Assert.Equal("DVT", project.CurrentStageCode);

        // 驗收條件 1：模擬資料的專案顯示目前在 DVT，延誤的關卡顯示紅字和天數
        var dvtStage = project.Stages.FirstOrDefault(s => s.StageCode == "DVT");
        Assert.NotNull(dvtStage);
        Assert.True(dvtStage.IsCurrent);

        var feasibilityStage = project.Stages.FirstOrDefault(s => s.StageCode == "Feasibility");
        Assert.NotNull(feasibilityStage);
        Assert.True(feasibilityStage.IsDelayed);
        Assert.Equal(5, feasibilityStage.DelayDays);

        var evtStage = project.Stages.FirstOrDefault(s => s.StageCode == "EVT");
        Assert.NotNull(evtStage);
        Assert.True(evtStage.IsDelayed);
        Assert.Equal(5, evtStage.DelayDays);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task StageTimeline_AfterStageRenamed_ShowsNewStageName()
    {
        await SeedSampleProjectAsync(TenantAId);

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

        // 2. 查詢專案詳情，驗收時間軸上的 EVT 名稱已即時反映為「工程樣品驗證」
        var projRes = await client.GetAsync("/api/projects/NPI-2601");
        projRes.EnsureSuccessStatusCode();

        var project = await projRes.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);

        var evtStage = project.Stages.FirstOrDefault(s => s.StageCode == "EVT");
        Assert.NotNull(evtStage);
        Assert.Equal("工程樣品驗證", evtStage.StageName);
    }
}


