using System.Net.Http.Json;
using Core.Data;
using IntegrationTests.Isolation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Host;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests;

public class DevUpTests : IsolationTestBase
{
    [Fact]
    [Trait("Category", "")]
    public async Task MockDataSeeder_SeedsInitialProjectsAndPersons()
    {
        await MockDataSeeder.SeedAsync(Factory.Services);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();

        using (dataFilter.Disable<IMultiTenant>())
        {
            var projects = await dbContext.StgProjects.ToListAsync();
            Assert.NotEmpty(projects);
            Assert.Contains(projects, p => p.ProjectName == "範例產品 A");
            Assert.Contains(projects, p => p.ProjectCode == "NPI-2601");
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task MockDataSeeder_WhenRunTwice_DoesNotDuplicateProjects()
    {
        // First run
        await MockDataSeeder.SeedAsync(Factory.Services);

        using var scope1 = Factory.Services.CreateScope();
        var dbContext1 = scope1.ServiceProvider.GetRequiredService<NpiDbContext>();
        var dataFilter1 = scope1.ServiceProvider.GetRequiredService<IDataFilter>();
        int initialCount;
        using (dataFilter1.Disable<IMultiTenant>())
        {
            initialCount = await dbContext1.StgProjects.CountAsync();
        }
        Assert.True(initialCount > 0);

        // Second run
        await MockDataSeeder.SeedAsync(Factory.Services);

        using var scope2 = Factory.Services.CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<NpiDbContext>();
        var dataFilter2 = scope2.ServiceProvider.GetRequiredService<IDataFilter>();
        int secondCount;
        using (dataFilter2.Disable<IMultiTenant>())
        {
            secondCount = await dbContext2.StgProjects.CountAsync();
        }

        Assert.Equal(initialCount, secondCount);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task GetDemoProjectsEndpoint_ReturnsSeededProjectNames()
    {
        await MockDataSeeder.SeedAsync(Factory.Services);

        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/demo/projects");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<DemoProjectsResponse>();
        Assert.NotNull(result);
        Assert.Contains("範例產品 A", result.Projects);
    }
}


