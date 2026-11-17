using Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Features;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;

namespace Core;

[DependsOn(
    typeof(AbpMultiTenancyModule),
    typeof(AbpFeaturesModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule)
)]
public class CoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<TenantRlsConnectionInterceptor>();

        context.Services.AddAbpDbContext<NpiDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        Configure<AbpDbContextOptions>(options =>
        {
            options.UseNpgsql();
        });
    }
}


