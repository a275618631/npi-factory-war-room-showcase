using Core.Data;
using Core.Projects;
using Core.Settings;
using IntegrationTests.Isolation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests.Settings;

public class StageSettingsTests : IsolationTestBase
{
    [Fact]
    [Trait("Category", "")]
    public async Task GetStages_ByDefault_ReturnsSixStagesInCorrectOrder()
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var stageService = scope.ServiceProvider.GetRequiredService<IStageSettingsService>();

        using (currentTenant.Change(TenantAId))
        {
            var stages = await stageService.GetStagesAsync();

            Assert.Equal(6, stages.Count);
            Assert.Equal("Concept", stages[0].Code);
            Assert.Equal("Feasibility", stages[1].Code);
            Assert.Equal("EVT", stages[2].Code);
            Assert.Equal("DVT", stages[3].Code);
            Assert.Equal("PVT", stages[4].Code);
            Assert.Equal("MP", stages[5].Code);

            Assert.All(stages, s => Assert.True(s.IsActive));
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task RenameStage_ToEngineeringSample_ReflectsNewNameAndIsTenantIsolated()
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var stageService = scope.ServiceProvider.GetRequiredService<IStageSettingsService>();

        // 1. 公司 A 將 EVT 改名成「工程樣品」
        using (currentTenant.Change(TenantAId))
        {
            var stagesA = await stageService.GetStagesAsync();
            var evt = stagesA.First(s => s.Code == "EVT");
            evt.Name = "工程樣品";
            evt.Color = "#1890FF";

            await stageService.SaveStagesAsync(stagesA);

            // 重新整理/重新取得設定
            var reloadedA = await stageService.GetStagesAsync();
            var updatedEvtA = reloadedA.First(s => s.Code == "EVT");
            Assert.Equal("工程樣品", updatedEvtA.Name);
            Assert.Equal("#1890FF", updatedEvtA.Color);
        }

        // 2. 驗證公司 B 不受影響（租戶隔離）
        using (currentTenant.Change(TenantBId))
        {
            var stagesB = await stageService.GetStagesAsync();
            var evtB = stagesB.First(s => s.Code == "EVT");
            Assert.Equal("工程驗證", evtB.Name); // 公司 B 仍為預設名稱
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task DisabledStage_CannotBeUsedByNewProjects_WhileOldProjectsRetainData()
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var stageService = scope.ServiceProvider.GetRequiredService<IStageSettingsService>();
        var projectService = scope.ServiceProvider.GetRequiredService<IProjectTransformService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();

        using (currentTenant.Change(TenantAId))
        {
            // 1. 先建立一個既有的舊專案（已在正式區），使用 Concept 階段
            var oldProject = new Project(Guid.NewGuid(), "PRJ-OLD-001", "既有專案A", "E001", "Concept", TenantAId);
            await dbContext.Projects.AddAsync(oldProject);
            await dbContext.SaveChangesAsync();

            // 2. 將 Concept 階段停用 (IsActive = false)
            var stages = await stageService.GetStagesAsync();
            var concept = stages.First(s => s.Code == "Concept");
            concept.IsActive = false;
            await stageService.SaveStagesAsync(stages);

            // 驗證 Concept 已非啟用階段
            var isActive = await stageService.IsStageActiveAsync("Concept");
            Assert.False(isActive);

            // 3. 嘗試匯入新專案，其 current_stage_code 為已停用的 Concept
            var batchId = Guid.NewGuid();
            var stgNewProject = new StgProject(Guid.NewGuid(), "PRJ-NEW-002", "新專案B", "E002", "Concept", TenantAId, batchId);
            await dbContext.StgProjects.AddAsync(stgNewProject);
            await dbContext.SaveChangesAsync();

            // 執行轉換
            var transformResult = await projectService.TransformAsync(batchId, DateOnly.FromDateTime(DateTime.UtcNow));

            // 斷言新專案轉換失敗
            Assert.Equal(0, transformResult.ProjectsImported);
            Assert.True(transformResult.Errors.Count > 0);
            Assert.Contains(transformResult.Errors, e => e.Contains("Concept") || e.Contains("current_stage_code"));

            // 4. 斷言舊專案依然完整存在於資料庫正式區，不受任何影響
            var existingOld = await dbContext.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectCode == "PRJ-OLD-001");
            Assert.NotNull(existingOld);
            Assert.Equal("Concept", existingOld.CurrentStageCode);
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task StageSettingsApi_GetAndPut_WorksCorrectly()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("__tenant", TenantAId.ToString());

        // 呼叫 GET /api/settings/stages
        var getRes = await client.GetAsync("/api/settings/stages");
        Assert.True(getRes.IsSuccessStatusCode);

        var stages = await getRes.Content.ReadFromJsonAsync<List<StageConfigItem>>();
        Assert.NotNull(stages);
        Assert.True(stages.Count >= 6);

        // 修改第一項並 PUT
        stages[0].Name = "創新概念";
        var putRes = await client.PutAsJsonAsync("/api/settings/stages", stages);
        Assert.True(putRes.IsSuccessStatusCode);

        var updated = await putRes.Content.ReadFromJsonAsync<List<StageConfigItem>>();
        Assert.NotNull(updated);
        Assert.Equal("創新概念", updated[0].Name);
    }
}


