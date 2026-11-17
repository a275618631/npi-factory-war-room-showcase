using Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests;

public class TenantFilterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = _dbContainer.GetConnectionString()
                    });
                });
            });

        // Initialize schema
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "")]
    public async Task TenantFilter_WhenQueryingAsTenantA_ReturnsOnlyTenantAItems()
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        var tenantA = new Tenant(Guid.NewGuid(), "Company A", "COMPANY_A");
        var tenantB = new Tenant(Guid.NewGuid(), "Company B", "COMPANY_B");

        await dbContext.Tenants.AddRangeAsync(tenantA, tenantB);
        await dbContext.SaveChangesAsync();

        // Seed 3 items for Tenant A
        using (currentTenant.Change(tenantA.Id))
        {
            await dbContext.DemoItems.AddRangeAsync(
                new DemoItem(Guid.NewGuid(), "Item A1", tenantA.Id),
                new DemoItem(Guid.NewGuid(), "Item A2", tenantA.Id),
                new DemoItem(Guid.NewGuid(), "Item A3", tenantA.Id)
            );
            await dbContext.SaveChangesAsync();
        }

        // Seed 3 items for Tenant B
        using (currentTenant.Change(tenantB.Id))
        {
            await dbContext.DemoItems.AddRangeAsync(
                new DemoItem(Guid.NewGuid(), "Item B1", tenantB.Id),
                new DemoItem(Guid.NewGuid(), "Item B2", tenantB.Id),
                new DemoItem(Guid.NewGuid(), "Item B3", tenantB.Id)
            );
            await dbContext.SaveChangesAsync();
        }

        // Query as Tenant A: must only see Tenant A's 3 items
        using (currentTenant.Change(tenantA.Id))
        {
            var itemsA = await dbContext.DemoItems.ToListAsync();
            Assert.Equal(3, itemsA.Count);
            Assert.All(itemsA, item => Assert.Equal(tenantA.Id, item.TenantId));
            Assert.Contains(itemsA, item => item.Name == "Item A1");
            Assert.Contains(itemsA, item => item.Name == "Item A2");
            Assert.Contains(itemsA, item => item.Name == "Item A3");
        }

        // Query as Tenant B: must only see Tenant B's 3 items
        using (currentTenant.Change(tenantB.Id))
        {
            var itemsB = await dbContext.DemoItems.ToListAsync();
            Assert.Equal(3, itemsB.Count);
            Assert.All(itemsB, item => Assert.Equal(tenantB.Id, item.TenantId));
            Assert.Contains(itemsB, item => item.Name == "Item B1");
            Assert.Contains(itemsB, item => item.Name == "Item B2");
            Assert.Contains(itemsB, item => item.Name == "Item B3");
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task TenantFilter_WhenNoCurrentTenant_ReturnsNoCompanyData()
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();

        var tenantA = new Tenant(Guid.NewGuid(), "Company Alpha", "ALPHA");
        var tenantB = new Tenant(Guid.NewGuid(), "Company Beta", "BETA");

        await dbContext.Tenants.AddRangeAsync(tenantA, tenantB);
        await dbContext.SaveChangesAsync();

        using (currentTenant.Change(tenantA.Id))
        {
            await dbContext.DemoItems.AddRangeAsync(
                new DemoItem(Guid.NewGuid(), "Alpha 1", tenantA.Id),
                new DemoItem(Guid.NewGuid(), "Alpha 2", tenantA.Id),
                new DemoItem(Guid.NewGuid(), "Alpha 3", tenantA.Id)
            );
            await dbContext.SaveChangesAsync();
        }

        using (currentTenant.Change(tenantB.Id))
        {
            await dbContext.DemoItems.AddRangeAsync(
                new DemoItem(Guid.NewGuid(), "Beta 1", tenantB.Id),
                new DemoItem(Guid.NewGuid(), "Beta 2", tenantB.Id),
                new DemoItem(Guid.NewGuid(), "Beta 3", tenantB.Id)
            );
            await dbContext.SaveChangesAsync();
        }

        // Query without current tenant (Host / no tenant): must return 0 items
        using (currentTenant.Change(null))
        {
            var items = await dbContext.DemoItems.ToListAsync();
            Assert.Empty(items);
        }

        // When data filter is disabled explicitly, all company items can be retrieved
        using (dataFilter.Disable<IMultiTenant>())
        {
            var allItems = await dbContext.DemoItems.ToListAsync();
            Assert.Equal(6, allItems.Count);
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task TenantFilter_WorksViaAbpRepository()
    {
        using var scope = _factory!.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<DemoItem, Guid>>();

        var tenantId = Guid.NewGuid();

        using (currentTenant.Change(tenantId))
        {
            await repository.InsertAsync(new DemoItem(Guid.NewGuid(), "Repo Item 1", tenantId));
            await repository.InsertAsync(new DemoItem(Guid.NewGuid(), "Repo Item 2", tenantId));

            var list = await repository.GetListAsync();
            Assert.Equal(2, list.Count);
            Assert.All(list, x => Assert.Equal(tenantId, x.TenantId));
        }

        // Query without tenant via repository
        using (currentTenant.Change(null))
        {
            var list = await repository.GetListAsync();
            Assert.DoesNotContain(list, x => x.TenantId == tenantId);
        }
    }
}


