using System.Net;
using System.Net.Http.Json;
using Core.Data;
using Core.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationTests;

public class ModuleToggleTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _dbContainer.GetConnectionString()
            }));
        });
        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NpiDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        if (_factory != null) await _factory.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "")]
    public async Task ModuleToggle_CompanyAEnabled_CompanyBDisabled_Returns200And404()
    {
        var tenantA = new Tenant(Guid.NewGuid(), "Company A", "COMPANY_A");
        var tenantB = new Tenant(Guid.NewGuid(), "Company B", "COMPANY_B");

        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
            var featureManager = scope.ServiceProvider.GetRequiredService<ITenantFeatureManager>();
            await dbContext.Tenants.AddRangeAsync(tenantA, tenantB);
            await dbContext.SaveChangesAsync();
            await featureManager.SetFeatureAsync(tenantA.Id, NpiFeatures.Module.N4, true);
            await featureManager.SetFeatureAsync(tenantB.Id, NpiFeatures.Module.N4, false);
        }

        // Tenant A (ID & code) returns 200 OK
        using var resA = await GetAsync("/api/n4/ping", tenantA.Id.ToString());
        Assert.Equal(HttpStatusCode.OK, resA.StatusCode);
        var ping = await resA.Content.ReadFromJsonAsync<PingResponse>();
        Assert.Equal("pong", ping!.Message);

        using var resACode = await GetAsync("/api/n4/ping", "COMPANY_A");
        Assert.Equal(HttpStatusCode.OK, resACode.StatusCode);

        // Tenant B (ID & code) returns 404 Not Found
        using var resB = await GetAsync("/api/n4/ping", tenantB.Id.ToString());
        Assert.Equal(HttpStatusCode.NotFound, resB.StatusCode);

        using var resBCode = await GetAsync("/api/n4/ping", "COMPANY_B");
        Assert.Equal(HttpStatusCode.NotFound, resBCode.StatusCode);

        // Enabled modules list for menu: Tenant A has N4, Tenant B does not
        using var modA = await GetAsync("/api/modules", tenantA.Id.ToString());
        var dataA = await modA.Content.ReadFromJsonAsync<EnabledModulesResponse>();
        Assert.Contains(NpiFeatures.Module.N4, dataA!.EnabledModules);

        using var modB = await GetAsync("/api/modules", tenantB.Id.ToString());
        var dataB = await modB.Content.ReadFromJsonAsync<EnabledModulesResponse>();
        Assert.DoesNotContain(NpiFeatures.Module.N4, dataB!.EnabledModules);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task ModuleToggle_ToggledAtRuntime_TakesEffectWithoutRestart()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Company Dynamic", "DYNAMIC_CO");
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NpiDbContext>();
            var featureManager = scope.ServiceProvider.GetRequiredService<ITenantFeatureManager>();
            await dbContext.Tenants.AddAsync(tenant);
            await dbContext.SaveChangesAsync();
            await featureManager.SetFeatureAsync(tenant.Id, NpiFeatures.Module.N4, false);
        }

        // Before toggle: 404 Not Found
        using var resBefore = await GetAsync("/api/n4/ping", tenant.Id.ToString());
        Assert.Equal(HttpStatusCode.NotFound, resBefore.StatusCode);

        // Toggle to true at runtime via PUT endpoint without restarting server
        using var putOn = new HttpRequestMessage(HttpMethod.Put, $"/api/modules/{NpiFeatures.Module.N4}")
        {
            Headers = { { "__tenant", tenant.Id.ToString() } },
            Content = JsonContent.Create(new UpdateModuleStateRequest(true))
        };
        using var putOnRes = await _client.SendAsync(putOn);
        Assert.Equal(HttpStatusCode.OK, putOnRes.StatusCode);

        // Immediately returns 200 OK without restart
        using var resAfterOn = await GetAsync("/api/n4/ping", tenant.Id.ToString());
        Assert.Equal(HttpStatusCode.OK, resAfterOn.StatusCode);

        using var modOn = await GetAsync("/api/modules", tenant.Id.ToString());
        var dataOn = await modOn.Content.ReadFromJsonAsync<EnabledModulesResponse>();
        Assert.Contains(NpiFeatures.Module.N4, dataOn!.EnabledModules);

        // Toggle back to false at runtime
        using var putOff = new HttpRequestMessage(HttpMethod.Put, $"/api/modules/{NpiFeatures.Module.N4}")
        {
            Headers = { { "__tenant", tenant.Id.ToString() } },
            Content = JsonContent.Create(new UpdateModuleStateRequest(false))
        };
        using var putOffRes = await _client.SendAsync(putOff);
        Assert.Equal(HttpStatusCode.OK, putOffRes.StatusCode);

        using var resAfterOff = await GetAsync("/api/n4/ping", tenant.Id.ToString());
        Assert.Equal(HttpStatusCode.NotFound, resAfterOff.StatusCode);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task ModuleToggle_WithoutTenantContext_Returns404()
    {
        using var res = await _client.GetAsync("/api/n4/ping");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        using var modRes = await _client.GetAsync("/api/modules");
        Assert.Equal(HttpStatusCode.OK, modRes.StatusCode);
        var data = await modRes.Content.ReadFromJsonAsync<EnabledModulesResponse>();
        Assert.DoesNotContain(NpiFeatures.Module.N4, data!.EnabledModules);
    }

    private async Task<HttpResponseMessage> GetAsync(string url, string tenant)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("__tenant", tenant);
        return await _client.SendAsync(req);
    }
}


