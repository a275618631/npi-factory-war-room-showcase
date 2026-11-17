using Core.Data;
using Core.Projects;
using Core.Settings;
using IntegrationTests.Isolation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Projects;

public class ProjectStageTests : IsolationTestBase
{
    [Fact]
    [Trait("Category", "")]
    public async Task DelayDays_MatchesHandCalculation_WithStandardDvtScenario()
    {
        // 驗收條件：模擬資料的延誤天數跟手算一致
        // 1. Concept: 2026-05-15 -> 2026-05-15 (0 days)
        // 2. Feasibility: 2026-07-15 -> 2026-07-20 (5 days)
        // 3. EVT: 2026-09-30 -> 2026-10-05 (5 days)
        // 4. DVT (進行中): 2026-12-15, 基準日 2026-11-20 (0 days)
        // 5. PVT (未開始): 2027-02-15, 基準日 2026-11-20 (0 days)
        // 6. MP (未開始): 2027-04-30, 基準日 2026-11-20 (0 days)

        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var transformService = sp.GetRequiredService<IProjectTransformService>();
            var batchId = Guid.NewGuid();

            // 匯入專案
            var stgProject = new StgProject(
                Guid.NewGuid(),
                projectCode: "NPI-2601",
                projectName: "範例產品 A",
                pmPersonCode: "E1001",
                currentStageCode: "DVT",
                tenantId: TenantAId,
                importBatchId: batchId)
            {
                CurrentBuild = "DVT-2",
                PlannedQty = 600,
                CustomerRef = "客戶 A"
            };
            await dbContext.StgProjects.AddAsync(stgProject);

            // 匯入階段計畫 (照 DvtScenarioGenerator 的標準資料)
            var stages = new[]
            {
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "Concept", new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 15),
                    actualStart: new DateOnly(2026, 4, 1), actualEnd: new DateOnly(2026, 5, 15), gateDate: new DateOnly(2026, 5, 18), gateOwnerPersonCode: "E1001", tenantId: TenantAId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "Feasibility", new DateOnly(2026, 5, 16), new DateOnly(2026, 7, 15),
                    actualStart: new DateOnly(2026, 5, 18), actualEnd: new DateOnly(2026, 7, 20), gateDate: new DateOnly(2026, 7, 22), gateOwnerPersonCode: "E1001", tenantId: TenantAId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "EVT", new DateOnly(2026, 7, 21), new DateOnly(2026, 9, 30),
                    actualStart: new DateOnly(2026, 7, 23), actualEnd: new DateOnly(2026, 10, 5), gateDate: new DateOnly(2026, 10, 8), gateOwnerPersonCode: "E1002", tenantId: TenantAId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "DVT", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 15),
                    actualStart: new DateOnly(2026, 10, 9), actualEnd: null, gateDate: new DateOnly(2026, 12, 20), gateOwnerPersonCode: "E1001", tenantId: TenantAId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "PVT", new DateOnly(2026, 12, 16), new DateOnly(2027, 2, 15),
                    actualStart: null, actualEnd: null, gateDate: new DateOnly(2027, 2, 20), gateOwnerPersonCode: "E1008", tenantId: TenantAId, importBatchId: batchId),
                new StgStagePlan(Guid.NewGuid(), "NPI-2601", "MP", new DateOnly(2027, 2, 16), new DateOnly(2027, 4, 30),
                    actualStart: null, actualEnd: null, gateDate: new DateOnly(2027, 5, 5), gateOwnerPersonCode: "E1001", tenantId: TenantAId, importBatchId: batchId)
            };
            await dbContext.StgStagePlans.AddRangeAsync(stages);
            await dbContext.SaveChangesAsync();

            // 執行轉換（基準日：2026-11-20，DVT 試產情境設定日）
            var asOfDate = new DateOnly(2026, 11, 20);
            var result = await transformService.TransformAsync(batchId, asOfDate);

            Assert.Equal(1, result.ProjectsImported);
            Assert.Equal(6, result.StagePlansImported);
            Assert.Empty(result.Errors);

            // 查詢轉換後的專案與階段
            var projectDetail = await transformService.GetProjectDetailAsync("NPI-2601", asOfDate);
            Assert.NotNull(projectDetail);
            Assert.Equal("NPI-2601", projectDetail.ProjectCode);
            Assert.Equal("DVT", projectDetail.CurrentStageCode);
            Assert.Equal(6, projectDetail.Stages.Count);

            // 驗證各階段手算延誤天數
            var concept = projectDetail.Stages.First(s => s.StageCode == "Concept");
            Assert.Equal(0, concept.DelayDays);
            Assert.False(concept.IsDelayed);
            Assert.False(concept.IsCurrent);

            var feasibility = projectDetail.Stages.First(s => s.StageCode == "Feasibility");
            Assert.Equal(5, feasibility.DelayDays); // 2026-07-20 - 2026-07-15 = 5
            Assert.True(feasibility.IsDelayed);

            var evt = projectDetail.Stages.First(s => s.StageCode == "EVT");
            Assert.Equal(5, evt.DelayDays); // 2026-10-05 - 2026-09-30 = 5
            Assert.True(evt.IsDelayed);

            var dvt = projectDetail.Stages.First(s => s.StageCode == "DVT");
            Assert.Equal(0, dvt.DelayDays); // 2026-11-20 < 2026-12-15 = 0
            Assert.False(dvt.IsDelayed);
            Assert.True(dvt.IsCurrent);

            var pvt = projectDetail.Stages.First(s => s.StageCode == "PVT");
            Assert.Equal(0, pvt.DelayDays);

            var mp = projectDetail.Stages.First(s => s.StageCode == "MP");
            Assert.Equal(0, mp.DelayDays);

            // 專案總延誤天數 = 5 + 5 = 10 天
            Assert.Equal(10, projectDetail.TotalDelayDays);

            // 額外測試：若今天推進到 2026-12-20（DVT 超過計畫結束日 2026-12-15 且未結束）
            var futureDate = new DateOnly(2026, 12, 20);
            var futureDetail = await transformService.GetProjectDetailAsync("NPI-2601", futureDate);
            Assert.NotNull(futureDetail);
            var futureDvt = futureDetail.Stages.First(s => s.StageCode == "DVT");
            Assert.Equal(5, futureDvt.DelayDays); // 2026-12-20 - 2026-12-15 = 5
            Assert.True(futureDvt.IsDelayed);
            Assert.Equal(15, futureDetail.TotalDelayDays);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task InvalidCurrentStageCode_ReportsError_DoesNotTransformProject()
    {
        // 驗收條件：current_stage_code 不存在時該列進錯誤報表
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var transformService = sp.GetRequiredService<IProjectTransformService>();
            var batchId = Guid.NewGuid();

            // 匯入一個 current_stage_code 不在設定中的專案（例如 "UNKNOWN_STAGE"）
            var invalidProject = new StgProject(
                Guid.NewGuid(),
                projectCode: "NPI-ERR",
                projectName: "錯誤階段專案",
                pmPersonCode: "E1001",
                currentStageCode: "UNKNOWN_STAGE",
                tenantId: TenantAId,
                importBatchId: batchId);

            await dbContext.StgProjects.AddAsync(invalidProject);
            await dbContext.SaveChangesAsync();

            var result = await transformService.TransformAsync(batchId);

            // 驗證有錯誤回傳，且未匯入到正式區
            Assert.NotEmpty(result.Errors);
            Assert.Equal(0, result.ProjectsImported);

            // 驗證錯誤記錄到 ImportError 表
            var errors = await dbContext.ImportErrors
                .AsNoTracking()
                .Where(e => e.ImportBatchId == batchId)
                .ToListAsync();

            Assert.NotEmpty(errors);
            var stageError = errors.First(e => e.FieldName == "current_stage_code");
            Assert.Equal("UNKNOWN_STAGE", stageError.OriginalValue);
            Assert.Contains("不存在於公司設定", stageError.Reason);

            // 正式區 projects 資料表不得含有此專案
            var savedProject = await dbContext.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProjectCode == "NPI-ERR");
            Assert.Null(savedProject);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task TenantIsolation_ProjectsAreStrictlyIsolatedBetweenTenants()
    {
        // 驗證多租戶隔離：TenantA 的專案 TenantB 看不到
        await ExecuteScopedAsync(TenantAId, async (dbContextA, spA) =>
        {
            var transformA = spA.GetRequiredService<IProjectTransformService>();
            var batchA = Guid.NewGuid();

            await dbContextA.StgProjects.AddAsync(new StgProject(
                Guid.NewGuid(), "PRJ-A", "公司A專案", "E1001", "DVT", tenantId: TenantAId, importBatchId: batchA));
            await dbContextA.SaveChangesAsync();

            await transformA.TransformAsync(batchA);
        });

        await ExecuteScopedAsync(TenantBId, async (dbContextB, spB) =>
        {
            var transformB = spB.GetRequiredService<IProjectTransformService>();

            var projectsB = await transformB.GetProjectsAsync();
            Assert.DoesNotContain(projectsB, p => p.ProjectCode == "PRJ-A");

            var detailB = await transformB.GetProjectDetailAsync("PRJ-A");
            Assert.Null(detailB);
        });
    }
}


