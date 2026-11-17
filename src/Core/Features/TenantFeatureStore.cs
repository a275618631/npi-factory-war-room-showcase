using Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Features;
using Volo.Abp.MultiTenancy;

namespace Core.Features;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IFeatureStore))]
public class EfCoreFeatureStore : IFeatureStore, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EfCoreFeatureStore(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string?> GetOrNullAsync(string name, string? providerName, string? providerKey)
    {
        if (providerName == TenantFeatureValueProvider.ProviderName && Guid.TryParse(providerKey, out var tenantId))
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
            var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();

            using (dataFilter.Disable<IMultiTenant>())
            {
                var setting = await dbContext.TenantFeatureSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Name == name);

                return setting?.Value;
            }
        }

        return null;
    }
}

public interface ITenantFeatureManager
{
    Task SetFeatureAsync(Guid tenantId, string name, bool isEnabled);
    Task<bool> IsFeatureEnabledAsync(Guid tenantId, string name);
    Task<IReadOnlyList<string>> GetEnabledModulesAsync();
}

public class TenantFeatureManager : ITenantFeatureManager, ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly IDataFilter _dataFilter;
    private readonly IFeatureChecker _featureChecker;
    private readonly IFeatureDefinitionManager _featureDefinitionManager;

    public TenantFeatureManager(
        NpiDbContext dbContext,
        IDataFilter dataFilter,
        IFeatureChecker featureChecker,
        IFeatureDefinitionManager featureDefinitionManager)
    {
        _dbContext = dbContext;
        _dataFilter = dataFilter;
        _featureChecker = featureChecker;
        _featureDefinitionManager = featureDefinitionManager;
    }

    public async Task SetFeatureAsync(Guid tenantId, string name, bool isEnabled)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var setting = await _dbContext.TenantFeatureSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Name == name);

            if (setting == null)
            {
                setting = new TenantFeatureSetting(Guid.NewGuid(), name, isEnabled.ToString().ToLowerInvariant(), tenantId);
                await _dbContext.TenantFeatureSettings.AddAsync(setting);
            }
            else
            {
                setting.Value = isEnabled.ToString().ToLowerInvariant();
            }

            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<bool> IsFeatureEnabledAsync(Guid tenantId, string name)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var setting = await _dbContext.TenantFeatureSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Name == name);

            if (setting != null)
            {
                return string.Equals(setting.Value, "true", StringComparison.OrdinalIgnoreCase);
            }

            var def = await _featureDefinitionManager.GetOrNullAsync(name);
            return string.Equals(def?.DefaultValue, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task<IReadOnlyList<string>> GetEnabledModulesAsync()
    {
        var allFeatures = await _featureDefinitionManager.GetAllAsync();
        var moduleFeatures = allFeatures.Where(f => f.Name.StartsWith("Module."));
        var enabledList = new List<string>();

        foreach (var feature in moduleFeatures)
        {
            if (await _featureChecker.IsEnabledAsync(feature.Name))
            {
                enabledList.Add(feature.Name);
            }
        }

        return enabledList;
    }
}


