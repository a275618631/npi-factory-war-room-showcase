using System.Reflection;
using Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests.Isolation;

public abstract class IsolationTestBase : IAsyncLifetime
{
    protected readonly PostgreSqlContainer DbContainer = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public Guid TenantAId { get; } = Guid.NewGuid();
    public Guid TenantBId { get; } = Guid.NewGuid();
    public Tenant TenantA { get; private set; } = null!;
    public Tenant TenantB { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        await DbContainer.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = DbContainer.GetConnectionString()
                });
            });
        });

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        TenantA = new Tenant(TenantAId, "Company A", "COMPANY_A");
        TenantB = new Tenant(TenantBId, "Company B", "COMPANY_B");
        await dbContext.Tenants.AddRangeAsync(TenantA, TenantB);
        await dbContext.SaveChangesAsync();

        await SeedDefaultDataAsync(dbContext, scope.ServiceProvider);
    }

    protected virtual async Task SeedDefaultDataAsync(NpiDbContext dbContext, IServiceProvider serviceProvider)
    {
        var currentTenant = serviceProvider.GetRequiredService<ICurrentTenant>();
        using (currentTenant.Change(TenantAId))
        {
            await dbContext.DemoItems.AddRangeAsync(
                new DemoItem(Guid.NewGuid(), "Item A1", TenantAId),
                new DemoItem(Guid.NewGuid(), "Item A2", TenantAId),
                new DemoItem(Guid.NewGuid(), "Item A3", TenantAId));
            await dbContext.SaveChangesAsync();
        }
        using (currentTenant.Change(TenantBId))
        {
            await dbContext.DemoItems.AddRangeAsync(
                new DemoItem(Guid.NewGuid(), "Item B1", TenantBId),
                new DemoItem(Guid.NewGuid(), "Item B2", TenantBId),
                new DemoItem(Guid.NewGuid(), "Item B3", TenantBId));
            await dbContext.SaveChangesAsync();
        }
    }

    public virtual async Task DisposeAsync()
    {
        if (Factory != null) await Factory.DisposeAsync();
        await DbContainer.DisposeAsync();
    }

    public IServiceScope CreateScope() => Factory.Services.CreateScope();

    public async Task ExecuteScopedAsync(Guid? tenantId, Func<NpiDbContext, IServiceProvider, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        using (currentTenant.Change(tenantId))
        {
            await action(dbContext, scope.ServiceProvider);
        }
    }

    public static async Task<List<object>> QueryEntitiesAsync(DbContext dbContext, Type entityType)
    {
        var setMethod = typeof(DbContext).GetMethods()
            .First(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethod && m.GetParameters().Length == 0)
            .MakeGenericMethod(entityType);
        var queryable = (IQueryable)setMethod.Invoke(dbContext, null)!;

        var toListAsyncMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        var task = (Task)toListAsyncMethod.Invoke(null, [queryable, CancellationToken.None])!;
        await task.ConfigureAwait(false);
        var resultProp = task.GetType().GetProperty("Result")!;
        return ((System.Collections.IEnumerable)resultProp.GetValue(task)!).Cast<object>().ToList();
    }

    public static async Task AssertTenantIsolationAsync(DbContext dbContext, Type entityType, Guid queryingTenantId, Guid otherTenantId)
    {
        var items = await QueryEntitiesAsync(dbContext, entityType);
        var leaked = items.OfType<IMultiTenant>().Where(x => x.TenantId == otherTenantId).ToList();
        if (leaked.Count > 0)
        {
            throw new Xunit.Sdk.XunitException(
                $"Isolation violation: Tenant {queryingTenantId} retrieved {leaked.Count} item(s) of Tenant {otherTenantId} for '{entityType.Name}'.");
        }
    }

    public static async Task EnsureTenantDataExistsAsync(DbContext dbContext, IDataFilter dataFilter, Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType, Guid tenantId)
    {
        using (dataFilter.Disable<IMultiTenant>())
        {
            var allItems = await QueryEntitiesAsync(dbContext, entityType.ClrType);
            if (allItems.OfType<IMultiTenant>().Any(x => x.TenantId == tenantId)) return;

            var instance = CreateSampleEntity(entityType, tenantId);
            dbContext.Add(instance);
            await dbContext.SaveChangesAsync();
        }
    }

    public static object CreateSampleEntity(Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType, Guid tenantId)
    {
        var type = entityType.ClrType;
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(c => c.GetParameters().Length).FirstOrDefault()
            ?? throw new InvalidOperationException($"No constructor for {type.FullName}");

        var parameters = ctor.GetParameters();
        var args = parameters.Select(p =>
            p.Name?.Equals("tenantId", StringComparison.OrdinalIgnoreCase) == true ? (object?)tenantId :
            p.ParameterType == typeof(Guid) || p.ParameterType == typeof(Guid?) ? Guid.NewGuid() :
            p.ParameterType == typeof(string) ? $"Sample_{p.Name}" :
            p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();

        var instance = parameters.Length == 0 ? Activator.CreateInstance(type, nonPublic: true)! : ctor.Invoke(args);

        foreach (var prop in entityType.GetProperties())
        {
            var propInfo = prop.PropertyInfo;
            if (propInfo == null || !propInfo.CanWrite) continue;

            if (prop.Name == nameof(IMultiTenant.TenantId)) propInfo.SetValue(instance, tenantId);
            else if (prop.IsPrimaryKey() && prop.ClrType == typeof(Guid))
            {
                var cur = (Guid?)propInfo.GetValue(instance);
                if (cur == null || cur == Guid.Empty) propInfo.SetValue(instance, Guid.NewGuid());
            }
            else if (prop.ClrType == typeof(string) && string.IsNullOrEmpty((string?)propInfo.GetValue(instance)))
            {
                propInfo.SetValue(instance, $"Sample_{prop.Name}");
            }
        }
        return instance;
    }
}


