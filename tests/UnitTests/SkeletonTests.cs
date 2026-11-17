using System.Linq;
using Core;
using Platform.Host;
using Volo.Abp.Modularity;
using Xunit;

namespace UnitTests;

public class SkeletonTests
{
    [Fact]
    [Trait("Category", "")]
    public void SolutionSkeleton_HostAndCoreModules_AreConfigured()
    {
        var hostModuleType = typeof(PlatformHostModule);
        Assert.True(typeof(AbpModule).IsAssignableFrom(hostModuleType));

        var dependsOn = hostModuleType.GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .FirstOrDefault();

        Assert.NotNull(dependsOn);
        Assert.Contains(typeof(CoreModule), dependsOn.DependedTypes);

        var coreModuleType = typeof(CoreModule);
        Assert.True(typeof(AbpModule).IsAssignableFrom(coreModuleType));
    }
}


