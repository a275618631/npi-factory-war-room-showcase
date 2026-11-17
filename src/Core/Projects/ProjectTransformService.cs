using Core.Data;
using Core.Import;
using Core.Settings;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Core.Projects;

public interface IProjectTransformService
{
    Task<ProjectTransformResult> TransformAsync(Guid? batchId = null, DateOnly? asOfDate = null, CancellationToken cancellationToken = default);
    Task<List<ProjectDto>> GetProjectsAsync(DateOnly? asOfDate = null, CancellationToken cancellationToken = default);
    Task<ProjectDto?> GetProjectDetailAsync(string projectCode, DateOnly? asOfDate = null, CancellationToken cancellationToken = default);
}

public class ProjectTransformService : IProjectTransformService, ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISettingCenterService _settingCenterService;
    private readonly IStageSettingsService? _stageSettingsService;

    public ProjectTransformService(
        NpiDbContext dbContext,
        ICurrentTenant currentTenant,
        ISettingCenterService settingCenterService, IStageSettingsService? stageSettingsService = null)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _settingCenterService = settingCenterService;
        _stageSettingsService = stageSettingsService;
    }

    public static int CalculateDelayDays(DateOnly plannedEnd, DateOnly? actualEnd, DateOnly asOfDate)
    {
        if (actualEnd.HasValue)
        {
            var diff = actualEnd.Value.DayNumber - plannedEnd.DayNumber;
            return diff > 0 ? diff : 0;
        }
        else
        {
            var diff = asOfDate.DayNumber - plannedEnd.DayNumber;
            return diff > 0 ? diff : 0;
        }
    }

    public async Task<ProjectTransformResult> TransformAsync(
        Guid? batchId = null,
        DateOnly? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProjectTransformResult();
        var tenantId = _currentTenant.Id;
        var referenceDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. 取得公司設定中啟用的階段清單
        var stageListStr = await _settingCenterService.GetValueAsync(SettingDefinitions.Keys.StageList, cancellationToken);
        if (string.IsNullOrWhiteSpace(stageListStr))
        {
            stageListStr = "Concept,Feasibility,EVT,DVT,PVT,MP";
        }
        var allowedStages = new HashSet<string>(
            stageListStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);

        // 2. 處理 StgProject -> Project
        var stgProjectsQuery = _dbContext.StgProjects.AsQueryable();
        if (batchId.HasValue)
        {
            stgProjectsQuery = stgProjectsQuery.Where(p => p.ImportBatchId == batchId.Value);
        }

        var stgProjects = await stgProjectsQuery.OrderBy(p => p.CreatedAt).ToListAsync(cancellationToken);
        var existingProjects = await _dbContext.Projects.ToListAsync(cancellationToken);
        var existingProjectMap = existingProjects.ToDictionary(p => p.ProjectCode, StringComparer.OrdinalIgnoreCase);
        var processedProjectSourceIds = new HashSet<Guid>(
            existingProjects.Where(p => p.SourceRecordId.HasValue).Select(p => p.SourceRecordId!.Value));

        var validProjectCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var stg in stgProjects)
        {
            // 驗證 current_stage_code 必須是設定裡啟用的階段
            if (!allowedStages.Contains(stg.CurrentStageCode))
            {
                var errorMsg = $"專案「{stg.ProjectCode}」的目前階段「{stg.CurrentStageCode}」不存在於公司設定的階段清單中（有效階段：{stageListStr}）";
                result.Errors.Add(errorMsg);

                var error = new ImportError(
                    Guid.NewGuid(),
                    stg.ImportBatchId,
                    rowNumber: 1,
                    fieldName: "current_stage_code",
                    originalValue: stg.CurrentStageCode,
                    reason: errorMsg,
                    suggestion: $"請將階段修改為設定中的有效階段：{stageListStr}",
                    tenantId: tenantId);

                await _dbContext.ImportErrors.AddAsync(error, cancellationToken);
                continue;
            }

            if (processedProjectSourceIds.Contains(stg.Id))
            {
                validProjectCodes.Add(stg.ProjectCode);
                continue;
            }

            if (existingProjectMap.TryGetValue(stg.ProjectCode, out var existing))
            {
                existing.ProjectName = stg.ProjectName;
                existing.PmPersonCode = stg.PmPersonCode;
                existing.CurrentStageCode = stg.CurrentStageCode;
                existing.CurrentBuild = stg.CurrentBuild;
                existing.PlannedQty = stg.PlannedQty;
                existing.CustomerRef = stg.CustomerRef;
                existing.SourceRecordId = stg.Id;
                existing.UpdatedAt = DateTime.UtcNow;
                result.ProjectsUpdated++;
            }
            else
            {
                var newProject = new Project(
                    Guid.NewGuid(),
                    stg.ProjectCode,
                    stg.ProjectName,
                    stg.PmPersonCode,
                    stg.CurrentStageCode,
                    tenantId: tenantId,
                    currentBuild: stg.CurrentBuild,
                    plannedQty: stg.PlannedQty,
                    customerRef: stg.CustomerRef,
                    sourceRecordId: stg.Id);

                await _dbContext.Projects.AddAsync(newProject, cancellationToken);
                existingProjectMap[stg.ProjectCode] = newProject;
                result.ProjectsImported++;
            }

            validProjectCodes.Add(stg.ProjectCode);
        }

        // 3. 處理 StgStagePlan -> StagePlan
        var stgStagePlansQuery = _dbContext.StgStagePlans.AsQueryable();
        if (batchId.HasValue)
        {
            stgStagePlansQuery = stgStagePlansQuery.Where(s => s.ImportBatchId == batchId.Value);
        }

        var stgStagePlans = await stgStagePlansQuery.OrderBy(s => s.CreatedAt).ToListAsync(cancellationToken);
        var existingStages = await _dbContext.StagePlans.ToListAsync(cancellationToken);
        var existingStageMap = existingStages.ToDictionary(
            s => $"{s.ProjectCode}_{s.StageCode}", StringComparer.OrdinalIgnoreCase);
        var processedStageSourceIds = new HashSet<Guid>(
            existingStages.Where(s => s.SourceRecordId.HasValue).Select(s => s.SourceRecordId!.Value));

        foreach (var stg in stgStagePlans)
        {
            if (processedStageSourceIds.Contains(stg.Id))
            {
                continue;
            }

            var delayDays = CalculateDelayDays(stg.PlannedEnd, stg.ActualEnd, referenceDate);
            var key = $"{stg.ProjectCode}_{stg.StageCode}";

            if (existingStageMap.TryGetValue(key, out var existing))
            {
                existing.PlannedStart = stg.PlannedStart;
                existing.PlannedEnd = stg.PlannedEnd;
                existing.ActualStart = stg.ActualStart;
                existing.ActualEnd = stg.ActualEnd;
                existing.GateDate = stg.GateDate;
                existing.GateOwnerPersonCode = stg.GateOwnerPersonCode;
                existing.DelayDays = delayDays;
                existing.SourceRecordId = stg.Id;
                existing.UpdatedAt = DateTime.UtcNow;
                result.StagePlansUpdated++;
            }
            else
            {
                var newStage = new StagePlan(
                    Guid.NewGuid(),
                    stg.ProjectCode,
                    stg.StageCode,
                    stg.PlannedStart,
                    stg.PlannedEnd,
                    actualStart: stg.ActualStart,
                    actualEnd: stg.ActualEnd,
                    gateDate: stg.GateDate,
                    gateOwnerPersonCode: stg.GateOwnerPersonCode,
                    delayDays: delayDays,
                    tenantId: tenantId,
                    sourceRecordId: stg.Id);

                await _dbContext.StagePlans.AddAsync(newStage, cancellationToken);
                existingStageMap[key] = newStage;
                result.StagePlansImported++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<List<ProjectDto>> GetProjectsAsync(DateOnly? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var referenceDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var projects = await _dbContext.Projects.AsNoTracking().OrderBy(p => p.ProjectCode).ToListAsync(cancellationToken);
        var stagePlans = await _dbContext.StagePlans.AsNoTracking().ToListAsync(cancellationToken);

        var stageMap = stagePlans.GroupBy(s => s.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string>? stageNameMap = null;
        if (_stageSettingsService != null)
        {
            var stageConfigs = await _stageSettingsService.GetStagesAsync(cancellationToken);
            stageNameMap = stageConfigs.ToDictionary(s => s.Code, s => s.Name, StringComparer.OrdinalIgnoreCase);
        }

        var list = new List<ProjectDto>();
        foreach (var p in projects)
        {
            stageMap.TryGetValue(p.ProjectCode, out var stages);
            stages ??= new List<StagePlan>();

            var stageDtos = stages
                .OrderBy(s => s.PlannedStart)
                .Select(s =>
                {
                    var delay = CalculateDelayDays(s.PlannedEnd, s.ActualEnd, referenceDate);
                    return new StagePlanDto(
                        s.StageCode,
                        (stageNameMap != null && stageNameMap.TryGetValue(s.StageCode, out var sn) && !string.IsNullOrWhiteSpace(sn) ? sn : s.StageCode),
                        s.PlannedStart,
                        s.PlannedEnd,
                        s.ActualStart,
                        s.ActualEnd,
                        s.GateDate,
                        s.GateOwnerPersonCode ?? p.PmPersonCode,
                        delay,
                        delay > 0,
                        s.StageCode.Equals(p.CurrentStageCode, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();

            var totalDelay = stageDtos.Sum(s => s.DelayDays);

            list.Add(new ProjectDto(
                p.ProjectCode,
                p.ProjectName,
                p.PmPersonCode,
                p.CurrentStageCode,
                p.CurrentBuild,
                p.PlannedQty,
                p.CustomerRef,
                totalDelay,
                stageDtos));
        }

        return list;
    }

    public async Task<ProjectDto?> GetProjectDetailAsync(string projectCode, DateOnly? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var projects = await GetProjectsAsync(asOfDate, cancellationToken);
        return projects.FirstOrDefault(p => p.ProjectCode.Equals(projectCode, StringComparison.OrdinalIgnoreCase));
    }
}


