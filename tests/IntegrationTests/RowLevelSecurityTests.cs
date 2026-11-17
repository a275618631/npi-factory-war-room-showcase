using System.Data;
using System.Data.Common;
using Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests;

public class RowLevelSecurityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    private const string AppUser = "npi_rls_app_user";
    private static readonly string AppPassword = Guid.NewGuid().ToString("N");

    private Guid _tenantAId;
    private Guid _tenantBId;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var adminConnectionString = _dbContainer.GetConnectionString();

        // 1. As superuser (postgres), create database schema and enable RLS
        var adminOptions = new DbContextOptionsBuilder<NpiDbContext>()
            .UseNpgsql(adminConnectionString)
            .Options;

        await using (var adminDbContext = new NpiDbContext(adminOptions))
        {
            await adminDbContext.Database.EnsureCreatedAsync();
            await DemoItemRls.ApplyRlsAsync(adminDbContext.Database);

            // 2. As superuser, create non-owner, non-BYPASSRLS application user and grant table privileges
            var createRoleSql = $"""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{AppUser}') THEN
                        CREATE ROLE {AppUser} WITH LOGIN PASSWORD '{AppPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                    END IF;
                END
                $$;
                GRANT USAGE ON SCHEMA public TO {AppUser};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {AppUser};
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {AppUser};
                """;
            await adminDbContext.Database.ExecuteSqlRawAsync(createRoleSql);
        }

        // 3. Build connection string using non-owner application user
        var appConnBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = AppUser,
            Password = AppPassword
        };
        var appConnectionString = appConnBuilder.ConnectionString;

        // 4. Configure WebApplicationFactory to connect with the non-owner app user
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = appConnectionString
                    });
                });
            });

        // 5. Seed Tenants and DemoItems using application DbContext
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        _tenantAId = Guid.NewGuid();
        _tenantBId = Guid.NewGuid();

        var tenantA = new Tenant(_tenantAId, "RLS Company A", "RLS_A");
        var tenantB = new Tenant(_tenantBId, "RLS Company B", "RLS_B");

        await dbContext.Tenants.AddRangeAsync(tenantA, tenantB);
        await dbContext.SaveChangesAsync();

        // Seed 3 items for Tenant A
        using (currentTenant.Change(_tenantAId))
        {
            await dbContext.DemoItems.AddRangeAsync(
                new DemoItem(Guid.NewGuid(), "Item A1", _tenantAId),
                new DemoItem(Guid.NewGuid(), "Item A2", _tenantAId),
                new DemoItem(Guid.NewGuid(), "Item A3", _tenantAId)
            );
            await dbContext.SaveChangesAsync();
        }

        // Seed 3 items for Tenant B
        using (currentTenant.Change(_tenantBId))
        {
            await dbContext.DemoItems.AddRangeAsync(
                new DemoItem(Guid.NewGuid(), "Item B1", _tenantBId),
                new DemoItem(Guid.NewGuid(), "Item B2", _tenantBId),
                new DemoItem(Guid.NewGuid(), "Item B3", _tenantBId)
            );
            await dbContext.SaveChangesAsync();
        }
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
    public async Task RawSqlSelect_WhenQueryingAsTenantA_ReturnsOnlyTenantAItems()
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(_tenantAId))
        {
            await dbContext.Database.OpenConnectionAsync();
            try
            {
                var conn = dbContext.Database.GetDbConnection();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM demo_items;";

                await using var reader = await cmd.ExecuteReaderAsync();
                var fetchedItems = new List<(string Name, Guid TenantId)>();
                while (await reader.ReadAsync())
                {
                    var name = reader["Name"].ToString()!;
                    var tenantId = (Guid)reader["TenantId"];
                    fetchedItems.Add((name, tenantId));
                }

                Assert.Equal(3, fetchedItems.Count);
                Assert.All(fetchedItems, item => Assert.Equal(_tenantAId, item.TenantId));
                Assert.Contains(fetchedItems, item => item.Name == "Item A1");
                Assert.Contains(fetchedItems, item => item.Name == "Item A2");
                Assert.Contains(fetchedItems, item => item.Name == "Item A3");
                Assert.DoesNotContain(fetchedItems, item => item.Name == "Item B1");
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task RawSqlSelect_WhenQueryingAsTenantB_ReturnsOnlyTenantBItems()
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(_tenantBId))
        {
            await dbContext.Database.OpenConnectionAsync();
            try
            {
                var conn = dbContext.Database.GetDbConnection();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM demo_items;";

                await using var reader = await cmd.ExecuteReaderAsync();
                var fetchedItems = new List<(string Name, Guid TenantId)>();
                while (await reader.ReadAsync())
                {
                    var name = reader["Name"].ToString()!;
                    var tenantId = (Guid)reader["TenantId"];
                    fetchedItems.Add((name, tenantId));
                }

                Assert.Equal(3, fetchedItems.Count);
                Assert.All(fetchedItems, item => Assert.Equal(_tenantBId, item.TenantId));
                Assert.Contains(fetchedItems, item => item.Name == "Item B1");
                Assert.Contains(fetchedItems, item => item.Name == "Item B2");
                Assert.Contains(fetchedItems, item => item.Name == "Item B3");
                Assert.DoesNotContain(fetchedItems, item => item.Name == "Item A1");
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task Write_WhenTenantATriesToWriteTenantBId_FailsViaRawSql()
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(_tenantAId))
        {
            await dbContext.Database.OpenConnectionAsync();
            try
            {
                var conn = dbContext.Database.GetDbConnection();
                await using var cmd = conn.CreateCommand();
                var maliciousId = Guid.NewGuid();
                cmd.CommandText = $"""
                    INSERT INTO demo_items ("Id", "Name", "TenantId", "CreatedAt")
                    VALUES ('{maliciousId}', 'Malicious SQL Item', '{_tenantBId}', now());
                    """;

                var ex = await Assert.ThrowsAnyAsync<PostgresException>(async () =>
                {
                    await cmd.ExecuteNonQueryAsync();
                });

                // PostgreSQL 42501 = insufficient_privilege / row-level security policy violation
                Assert.Equal("42501", ex.SqlState);
                Assert.Contains("demo_items", ex.MessageText);
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task Write_WhenTenantATriesToWriteTenantBId_FailsViaEfCore()
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(_tenantAId))
        {
            var maliciousItem = new DemoItem(Guid.NewGuid(), "Malicious EF Item", _tenantBId);
            await dbContext.DemoItems.AddAsync(maliciousItem);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await dbContext.SaveChangesAsync();
            });

            var pgEx = ex.InnerException as PostgresException;
            Assert.NotNull(pgEx);
            Assert.Equal("42501", pgEx.SqlState);
            Assert.Contains("demo_items", pgEx.MessageText);
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task Write_WhenTenantAWritesTenantAItem_Succeeds()
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(_tenantAId))
        {
            var validItem = new DemoItem(Guid.NewGuid(), "Valid Item A4", _tenantAId);
            await dbContext.DemoItems.AddAsync(validItem);
            await dbContext.SaveChangesAsync();

            var saved = await dbContext.DemoItems.FirstOrDefaultAsync(x => x.Id == validItem.Id);
            Assert.NotNull(saved);
            Assert.Equal("Valid Item A4", saved.Name);
            Assert.Equal(_tenantAId, saved.TenantId);
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task Query_EvenWhenDataFilterIsDisabled_DatabaseRlsEnforcesIsolation()
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();

        using (currentTenant.Change(_tenantAId))
        {
            // Explicitly disable ABP application-level filter
            using (dataFilter.Disable<IMultiTenant>())
            {
                // Without RLS, this would return items from both tenants.
                // With RLS, the database enforces isolation, so only Tenant A's items are returned!
                var items = await dbContext.DemoItems.ToListAsync();
                Assert.Equal(3, items.Count);
                Assert.All(items, item => Assert.Equal(_tenantAId, item.TenantId));
                Assert.DoesNotContain(items, item => item.TenantId == _tenantBId);
            }
        }
    }

    [Fact]
    [Trait("Category", "")]
    public async Task RawSqlSelect_WhenNoTenant_ReturnsNoCompanyData()
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(null))
        {
            await dbContext.Database.OpenConnectionAsync();
            try
            {
                var conn = dbContext.Database.GetDbConnection();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM demo_items;";

                await using var reader = await cmd.ExecuteReaderAsync();
                var count = 0;
                while (await reader.ReadAsync())
                {
                    count++;
                }

                Assert.Equal(0, count);
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}



