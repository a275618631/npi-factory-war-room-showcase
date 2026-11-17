using Core.Data;
using Core.Settings;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Core.Projects;

public record BreadcrumbNodeDto(
    string Level,
    string Code,
    string Name,
    string Path);

public record DeepLinkContextResponse(
    bool HasPermission,
    string? ErrorMessage,
    List<BreadcrumbNodeDto> Breadcrumb);

public interface IDeepLinkService
{
    Task<DeepLinkContextResponse> GetContextAsync(
        string projectCode,
        string? stageCode = null,
        string? unitCode = null,
        string? itemType = null,
        string? itemCode = null,
        CancellationToken cancellationToken = default);
}

public class DeepLinkService : IDeepLinkService, ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IStageSettingsService _stageSettingsService;

    public DeepLinkService(
        NpiDbContext dbContext,
        ICurrentTenant currentTenant,
        IStageSettingsService stageSettingsService)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _stageSettingsService = stageSettingsService;
    }

    public async Task<DeepLinkContextResponse> GetContextAsync(
        string projectCode,
        string? stageCode = null,
        string? unitCode = null,
        string? itemType = null,
        string? itemCode = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.Id;
        if (tenantId == null)
        {
            return new DeepLinkContextResponse(false, "沒有權限", new List<BreadcrumbNodeDto>());
        }

        // 1. 檢查目前租戶是否有該專案權限
        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProjectCode == projectCode, cancellationToken);

        if (project == null)
        {
            // 權限不符或專案不存在：回傳沒有權限，絕不包含專案名稱
            return new DeepLinkContextResponse(false, "沒有權限", new List<BreadcrumbNodeDto>());
        }

        var breadcrumbs = new List<BreadcrumbNodeDto>
        {
            new("L0", "overview", "全部專案", "/overview"),
            new("L1", project.ProjectCode, $"{project.ProjectCode} {project.ProjectName}", $"/p/{project.ProjectCode}")
        };

        // 2. 如果直接開啟 L4，未傳入 stageCode 或 unitCode 時，自動依 itemCode 補齊完整路徑
        if (!string.IsNullOrWhiteSpace(itemCode))
        {
            if (string.IsNullOrWhiteSpace(stageCode))
            {
                if (itemCode.StartsWith("CON-", StringComparison.OrdinalIgnoreCase)) stageCode = "Concept";
                else if (itemCode.StartsWith("FEA-", StringComparison.OrdinalIgnoreCase)) stageCode = "Feasibility";
                else if (itemCode.StartsWith("EVT-", StringComparison.OrdinalIgnoreCase)) stageCode = "EVT";
                else if (itemCode.StartsWith("DVT-", StringComparison.OrdinalIgnoreCase)) stageCode = "DVT";
                else if (itemCode.StartsWith("PVT-", StringComparison.OrdinalIgnoreCase)) stageCode = "PVT";
                else if (itemCode.StartsWith("MP-", StringComparison.OrdinalIgnoreCase)) stageCode = "MP";
                else stageCode = project.CurrentStageCode ?? "DVT";
            }

            if (string.IsNullOrWhiteSpace(unitCode))
            {
                if (itemCode.Contains("PPAP", StringComparison.OrdinalIgnoreCase)) unitCode = "SQE";
                else if (itemCode.Contains("EE", StringComparison.OrdinalIgnoreCase)) unitCode = "EE";
                else if (itemCode.Contains("ME", StringComparison.OrdinalIgnoreCase)) unitCode = "ME";
                else unitCode = "PM";
            }
        }

        // 3. 處理 L2 關卡
        if (!string.IsNullOrWhiteSpace(stageCode))
        {
            var stageConfigs = await _stageSettingsService.GetStagesAsync(cancellationToken);
            var stageConfig = stageConfigs.FirstOrDefault(s => s.Code.Equals(stageCode, StringComparison.OrdinalIgnoreCase));
            var stageName = stageConfig?.Name ?? stageCode;

            breadcrumbs.Add(new("L2", stageCode, stageName, $"/p/{project.ProjectCode}/g/{stageCode}"));

            // 4. 處理 L3 單位
            if (!string.IsNullOrWhiteSpace(unitCode))
            {
                var orgUnit = await _dbContext.OrgUnits
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitCode == unitCode, cancellationToken);

                var unitName = orgUnit != null && !string.IsNullOrWhiteSpace(orgUnit.UnitName)
                    ? orgUnit.UnitName
                    : (unitCode.Equals("SQE", StringComparison.OrdinalIgnoreCase) ? "供應商品質" : unitCode);

                breadcrumbs.Add(new("L3", unitCode, $"{unitCode} {unitName}", $"/p/{project.ProjectCode}/g/{stageCode}/u/{unitCode}"));

                // 5. 處理 L4 細項工作
                if (!string.IsNullOrWhiteSpace(itemType) && !string.IsNullOrWhiteSpace(itemCode))
                {
                    string itemName = itemCode;
                    if (itemCode.Equals("DVT-PPAP", StringComparison.OrdinalIgnoreCase))
                    {
                        itemName = "PPAP 文件";
                    }

                    breadcrumbs.Add(new("L4", itemCode, $"{itemCode} {itemName}", $"/p/{project.ProjectCode}/item/{itemType}/{itemCode}"));
                }
            }
        }

        return new DeepLinkContextResponse(true, null, breadcrumbs);
    }
}


