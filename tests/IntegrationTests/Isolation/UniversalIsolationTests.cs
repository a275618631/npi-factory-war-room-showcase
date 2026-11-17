using Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;
using Xunit.Sdk;

namespace IntegrationTests.Isolation;

/// <summary>
/// A counterpart DbContext without ABP multi-tenancy global query filters.
/// Used for negative testing: proving that when the tenant filter is removed or absent,
/// the isolation test detects the cross-tenant leakage and fails.
/// </summary>
public class UnfilteredNpiDbContext : DbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<DemoItem> DemoItems => Set<DemoItem>();

    public UnfilteredNpiDbContext(DbContextOptions<UnfilteredNpiDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Tenant>(b =>
        {
            b.ToTable("tenants");
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).IsRequired().HasMaxLength(128);
            b.Property(t => t.Code).IsRequired().HasMaxLength(64);
        });
        builder.Entity<DemoItem>(b =>
        {
            b.ToTable("demo_items");
            b.HasKey(d => d.Id);
            b.Property(d => d.Name).IsRequired().HasMaxLength(128);
            b.Property(d => d.TenantId).IsRequired();
            // Deliberately NO query filter configured (counterpart for negative testing)
        });
    }
}

public class UniversalIsolationTests : IsolationTestBase
{
    private UnfilteredNpiDbContext CreateUnfilteredDbContext()
    {
        var options = new DbContextOptionsBuilder<UnfilteredNpiDbContext>()
            .UseNpgsql(DbContainer.GetConnectionString())
            .Options;
        return new UnfilteredNpiDbContext(options);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task AllMultiTenantEntities_WhenQueryingAsTenantA_DoNotLeakTenantBData()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();

        // Universal discovery: find all entities in NpiDbContext implementing IMultiTenant
        var multiTenantEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(t => typeof(IMultiTenant).IsAssignableFrom(t.ClrType))
            .ToList();

        Assert.NotEmpty(multiTenantEntityTypes);

        foreach (var entityType in multiTenantEntityTypes)
        {
            await EnsureTenantDataExistsAsync(dbContext, dataFilter, entityType, TenantBId);
            using (currentTenant.Change(TenantAId))
            {
                await AssertTenantIsolationAsync(dbContext, entityType.ClrType, TenantAId, TenantBId);
            }
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task AllMultiTenantEntities_WhenQueryingAsTenantB_DoNotLeakTenantAData()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();

        var multiTenantEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(t => typeof(IMultiTenant).IsAssignableFrom(t.ClrType))
            .ToList();

        Assert.NotEmpty(multiTenantEntityTypes);

        foreach (var entityType in multiTenantEntityTypes)
        {
            await EnsureTenantDataExistsAsync(dbContext, dataFilter, entityType, TenantAId);
            using (currentTenant.Change(TenantBId))
            {
                await AssertTenantIsolationAsync(dbContext, entityType.ClrType, TenantBId, TenantAId);
            }
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task WhenFilterIsRemoved_IsolationTestFails()
    {
        await using var unfilteredContext = CreateUnfilteredDbContext();

        // Proves that when the filter on DemoItem is removed, the isolation assertion fails!
        var ex = await Assert.ThrowsAsync<XunitException>(async () =>
        {
            await AssertTenantIsolationAsync(unfilteredContext, typeof(DemoItem), TenantAId, TenantBId);
        });

        Assert.Contains("Isolation violation", ex.Message);
        Assert.Contains("DemoItem", ex.Message);
    }

    [Fact]
    [Trait("Category", "")]
    public void UniversalDiscovery_IdentifiesAllMultiTenantEntitiesInModel()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();

        var multiTenantTypes = dbContext.Model.GetEntityTypes()
            .Where(t => typeof(IMultiTenant).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        // Verify DemoItem is discovered automatically
        Assert.Contains(typeof(DemoItem), multiTenantTypes);
        // Verify non-multi-tenant host entities like Tenant are NOT discovered
        Assert.DoesNotContain(typeof(Tenant), multiTenantTypes);

        foreach (var clrType in multiTenantTypes)
        {
            Assert.True(typeof(IMultiTenant).IsAssignableFrom(clrType));
            Assert.NotNull(clrType.GetProperty(nameof(IMultiTenant.TenantId)));
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task IsolationTestBase_TenantSwitching_ChangesCurrentTenantCorrectly()
    {
        Assert.NotEqual(Guid.Empty, TenantAId);
        Assert.NotEqual(Guid.Empty, TenantBId);
        Assert.NotEqual(TenantAId, TenantBId);
        Assert.NotNull(TenantA);
        Assert.NotNull(TenantB);

        await ExecuteScopedAsync(TenantAId, (_, sp) =>
        {
            Assert.Equal(TenantAId, sp.GetRequiredService<ICurrentTenant>().Id);
            return Task.CompletedTask;
        });

        await ExecuteScopedAsync(TenantBId, (_, sp) =>
        {
            Assert.Equal(TenantBId, sp.GetRequiredService<ICurrentTenant>().Id);
            return Task.CompletedTask;
        });

        await ExecuteScopedAsync(null, (_, sp) =>
        {
            Assert.Null(sp.GetRequiredService<ICurrentTenant>().Id);
            return Task.CompletedTask;
        });
    }
}


