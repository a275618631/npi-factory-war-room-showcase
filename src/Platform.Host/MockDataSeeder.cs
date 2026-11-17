using Core.Data;
using Core.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MockData;
using Volo.Abp.MultiTenancy;

namespace Platform.Host;

public static class MockDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var env = sp.GetRequiredService<IHostEnvironment>();
        var config = sp.GetRequiredService<IConfiguration>();

        var shouldSeed = env.IsDevelopment() || config.GetValue<bool>("SEED_MOCK_DATA");
        if (!shouldSeed)
        {
            return;
        }

        var dbContext = sp.GetRequiredService<NpiDbContext>();
        try
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
        catch (Exception)
        {
            // If the database is unreachable at startup (e.g. during health check negative tests),
            // do not crash host startup so health checks can properly report Unhealthy.
            return;
        }

        var demoTenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Code == "DEMO" || t.Code == "DEMO");
        if (demoTenant == null)
        {
            demoTenant = new Tenant(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Demo Company", "DEMO");
            await dbContext.Tenants.AddAsync(demoTenant);
            await dbContext.SaveChangesAsync();
        }

        var currentTenant = sp.GetRequiredService<ICurrentTenant>();
        using (currentTenant.Change(demoTenant.Id))
        {
            if (await dbContext.StgProjects.AnyAsync())
            {
                // Already seeded, do not duplicate
                return;
            }

            var options = new ScenarioOptions { Seed = 20261103 };
            var dataSet = DvtScenarioGenerator.Generate(options);

            // Import Persons via var tempDir = Path.Combine(Path.GetTempPath(), "dev_seed_" + Guid.NewGuid().ToString("N"));
            try
            {
                DvtScenarioGenerator.WriteCsvFiles(dataSet, tempDir);
                var personCsvPath = Path.Combine(tempDir, "stg_person.csv");
                if (File.Exists(personCsvPath))
                {
                    var personImportService = sp.GetRequiredService<PersonImportService>();
                    await DvtScenarioGenerator.ImportPersonsAsync(personImportService, personCsvPath);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }

            // Seed Projects
            var batchId = Guid.NewGuid();
            foreach (var p in dataSet.Projects)
            {
                var stgProj = new StgProject(
                    Guid.NewGuid(),
                    p.project_code,
                    p.project_name,
                    p.pm_person_code,
                    p.current_stage_code,
                    demoTenant.Id,
                    batchId)
                {
                    CurrentBuild = p.current_build,
                    PlannedQty = p.planned_qty,
                    CustomerRef = p.customer_ref
                };
                await dbContext.StgProjects.AddAsync(stgProj);
            }
            await dbContext.SaveChangesAsync();
        }
    }
}


