using Core;
using Core.Audit;
using Core.Data;
using Core.Features;
using Core.Import;
using Core.Org;
using Core.Projects;
using Core.Settings;
using Core.UserPreference;
using Volo.Abp.Users;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Validation.AspNetCore;
using Platform.Host;
using Volo.Abp;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Features;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.Security.Claims;
using Volo.Abp.Settings;
using Volo.Abp.UI.Navigation.Urls;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseAutofac();
await builder.AddApplicationAsync<PlatformHostModule>();
var app = builder.Build();
await app.InitializeApplicationAsync();
await MockDataSeeder.SeedAsync(app.Services);
await app.RunAsync();

namespace Platform.Host
{
    [DependsOn(
        typeof(AbpAspNetCoreMvcModule),
        typeof(AbpAutofacModule),
        typeof(AbpIdentityEntityFrameworkCoreModule),
        typeof(AbpOpenIddictEntityFrameworkCoreModule),
        typeof(AbpAccountWebOpenIddictModule),
        typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
        typeof(CoreModule)
    )]
    public class PlatformHostModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            PreConfigure<OpenIddictBuilder>(builder =>
            {
                builder.AddValidation(options =>
                {
                    options.AddAudiences("Npi");
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });
                builder.AddServer(options =>
                {
                    options.UseAspNetCore().DisableTransportSecurityRequirement();
                });
            });
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();

            context.Services.AddOpenApi();
            context.Services.AddHealthChecks()
                .AddCheck<PostgreSqlHealthCheck>("postgres");

            context.Services.AddTransient<IFeatureDefinitionProvider, NpiFeatureDefinitionProvider>();
            context.Services.AddTransient<IFeatureValueProvider, TenantFeatureValueProvider>();
            context.Services.AddScoped<ITenantFeatureManager, TenantFeatureManager>();

            context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            Configure<AbpClaimsPrincipalFactoryOptions>(options =>
            {
                options.IsDynamicClaimsEnabled = true;
            });

            Configure<AbpLocalizationOptions>(options =>
            {
                options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "繁體中文"));
                options.Languages.Add(new LanguageInfo("en", "en", "English"));
            });

            Configure<AppUrlOptions>(options =>
            {
                options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"] ?? "http://localhost:5000";
                var redirectUrls = configuration["App:RedirectAllowedUrls"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    ?? new[] { "http://localhost:5173", "http://localhost:5173/callback", "http://localhost:5000" };
                options.RedirectAllowedUrls.AddRange(redirectUrls);
            });

            context.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins("http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            context.Services.AddAbpDbContext<NpiIdentityDbContext>(options =>
            {
                options.AddDefaultRepositories(includeAllEntities: true);
            });
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var env = context.GetEnvironment();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCorrelationId();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors();
            app.UseAuthentication();
            app.UseAbpOpenIddictValidation();
            app.UseUnitOfWork();
            app.UseAuthorization();

            app.Use(async (httpContext, next) =>
            {
                var currentTenant = httpContext.RequestServices.GetRequiredService<ICurrentTenant>();

                string? tenantHeader = null;
                if (httpContext.Request.Headers.TryGetValue("__tenant", out var tVal) && !string.IsNullOrWhiteSpace(tVal))
                {
                    tenantHeader = tVal.ToString();
                }
                else if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var xVal) && !string.IsNullOrWhiteSpace(xVal))
                {
                    tenantHeader = xVal.ToString();
                }

                if (!string.IsNullOrWhiteSpace(tenantHeader))
                {
                    Guid? tenantId = null;
                    if (Guid.TryParse(tenantHeader, out var parsedGuid))
                    {
                        tenantId = parsedGuid;
                    }
                    else
                    {
                        var dbContext = httpContext.RequestServices.GetRequiredService<NpiDbContext>();
                        var tenant = await dbContext.Tenants.AsNoTracking()
                            .FirstOrDefaultAsync(t => t.Code == tenantHeader || t.Name == tenantHeader);
                        if (tenant != null)
                        {
                            tenantId = tenant.Id;
                        }
                    }

                    if (tenantId.HasValue)
                    {
                        using (currentTenant.Change(tenantId.Value))
                        {
                            await next();
                            return;
                        }
                    }
                }

                await next();
            });

            app.UseConfiguredEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health", new HealthCheckOptions
                {
                    ResponseWriter = async (httpContext, report) =>
                    {
                        httpContext.Response.ContentType = "application/json";
                        var response = new
                        {
                            status = report.Status.ToString(),
                            database = report.Entries.TryGetValue("postgres", out var entry)
                                ? entry.Status.ToString()
                                : report.Status.ToString(),
                            description = report.Entries.TryGetValue("postgres", out var e)
                                ? e.Description
                                : null
                        };
                        await httpContext.Response.WriteAsJsonAsync(response);
                    }
                });

                endpoints.MapGet("/api/demo/ping", () => new PingResponse("pong", DateTime.UtcNow))
                    .WithName("GetDemoPing")
                    .WithTags("Demo")
                    .Produces<PingResponse>(StatusCodes.Status200OK);

                endpoints.MapGet("/api/demo/projects", async (NpiDbContext dbContext, ICurrentTenant currentTenant) =>
                {
                    var demoTenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Code == "DEMO" || t.Code == "DEMO");
                    var targetTenantId = currentTenant.Id ?? demoTenant?.Id;

                    if (targetTenantId == null)
                    {
                        return Results.Ok(new DemoProjectsResponse(Array.Empty<string>()));
                    }

                    using (currentTenant.Change(targetTenantId))
                    {
                        var projectNames = await dbContext.StgProjects
                            .Select(p => p.ProjectName)
                            .ToListAsync();
                        return Results.Ok(new DemoProjectsResponse(projectNames));
                    }
                })
                .WithName("GetDemoProjects")
                .WithTags("Demo")
                .Produces<DemoProjectsResponse>(StatusCodes.Status200OK);

                endpoints.MapGet("/api/n4/ping", async (IFeatureChecker featureChecker) =>
                {
                    if (!await featureChecker.IsEnabledAsync(NpiFeatures.Module.N4))
                    {
                        return Results.NotFound(new { message = "Module N4 is not enabled." });
                    }
                    return Results.Ok(new PingResponse("pong", DateTime.UtcNow));
                })
                .WithName("GetN4Ping")
                .WithTags("N4")
                .Produces<PingResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

                endpoints.MapGet("/api/modules", async (ITenantFeatureManager featureManager) =>
                {
                    var modules = await featureManager.GetEnabledModulesAsync();
                    return Results.Ok(new EnabledModulesResponse(modules));
                })
                .WithName("GetEnabledModules")
                .WithTags("Modules")
                .Produces<EnabledModulesResponse>(StatusCodes.Status200OK);

                endpoints.MapPut("/api/modules/{name}", async (
                    string name,
                    UpdateModuleStateRequest request,
                    ICurrentTenant currentTenant,
                    ITenantFeatureManager featureManager) =>
                {
                    if (currentTenant.Id == null)
                    {
                        return Results.BadRequest(new { message = "Tenant context is required." });
                    }
                    await featureManager.SetFeatureAsync(currentTenant.Id.Value, name, request.IsEnabled);
                    return Results.Ok(new { name, isEnabled = request.IsEnabled });
                })
                .WithName("UpdateModuleState")
                .WithTags("Modules")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);

                endpoints.MapGet("/api/org/tree", async (ICurrentTenant currentTenant, IOrgTransformService orgService) =>
                {
                    if (currentTenant.Id == null)
                    {
                        return Results.Ok(new List<OrgTreeNodeDto>());
                    }
                    var tree = await orgService.GetOrgTreeAsync();
                    return Results.Ok(tree);
                })
                .WithName("GetOrgTree")
                .WithTags("Org")
                .Produces<List<OrgTreeNodeDto>>(StatusCodes.Status200OK);

                endpoints.MapPost("/api/import/{table}", async (
                    string table,
                    BatchImportPayload payload,
                    HttpContext httpContext,
                    ICurrentTenant currentTenant,
                    IBatchImportService importService) =>
                {
                    string? apiKey = null;
                    if (httpContext.Request.Headers.TryGetValue("X-Api-Key", out var keyVal) && !string.IsNullOrWhiteSpace(keyVal))
                    {
                        apiKey = keyVal.ToString();
                    }
                    else if (httpContext.Request.Headers.TryGetValue("Authorization", out var authVal) && !string.IsNullOrWhiteSpace(authVal))
                    {
                        var authStr = authVal.ToString();
                        if (authStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            apiKey = authStr.Substring("Bearer ".Length).Trim();
                        }
                    }

                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        return Results.Unauthorized();
                    }

                    var tenantId = await importService.AuthenticateApiKeyAsync(apiKey);
                    if (tenantId == null)
                    {
                        return Results.Unauthorized();
                    }

                    using (currentTenant.Change(tenantId.Value))
                    {
                        var result = await importService.ImportBatchAsync(table, payload, tenantId.Value);
                        return Results.Ok(result);
                    }
                })
                .WithName("ImportBatch")
                .WithTags("Import")
                .Produces<BatchImportResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized);

                endpoints.MapGet("/api/import/batches", async (
                    int? take,
                    IImportHistoryService historyService) =>
                {
                    var batches = await historyService.GetBatchesAsync(take ?? 50);
                    return Results.Ok(batches);
                })
                .WithName("GetImportBatches")
                .WithTags("Import")
                .Produces<List<ImportBatchDto>>(StatusCodes.Status200OK);

                endpoints.MapGet("/api/import/batches/{batchId:guid}", async (
                    Guid batchId,
                    IImportHistoryService historyService) =>
                {
                    var detail = await historyService.GetBatchDetailAsync(batchId);
                    if (detail == null)
                    {
                        return Results.NotFound();
                    }
                    return Results.Ok(detail);
                })
                .WithName("GetImportBatchDetail")
                .WithTags("Import")
                .Produces<ImportBatchDetailDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

                endpoints.MapGet("/api/import/batches/{batchId:guid}/error-report", async (
                    Guid batchId,
                    IImportHistoryService historyService) =>
                {
                    var excelBytes = await historyService.DownloadErrorReportExcelAsync(batchId);
                    if (excelBytes == null)
                    {
                        return Results.NotFound();
                    }
                    return Results.File(
                        excelBytes,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"import_errors_{batchId}.xlsx");
                })
                .WithName("DownloadImportErrorReport")
                .WithTags("Import")
                .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                .Produces(StatusCodes.Status404NotFound);

                endpoints.MapGet("/api/audit-logs", async (
                    string? category,
                    string? target,
                    int? take,
                    IAuditLogService auditLogService) =>
                {
                    var logs = await auditLogService.QueryAsync(category, target, take ?? 50);
                    return Results.Ok(logs);
                })
                .WithName("GetAuditLogs")
                .WithTags("Audit")
                .Produces<List<AuditLogDto>>(StatusCodes.Status200OK);

                endpoints.MapGet("/api/projects", async (
                    IProjectTransformService projectService,
                    ICurrentTenant currentTenant) =>
                {
                    if (currentTenant.Id == null)
                    {
                        return Results.Ok(new List<ProjectDto>());
                    }
                    var projects = await projectService.GetProjectsAsync();
                    return Results.Ok(projects);
                })
                .WithName("GetProjects")
                .WithTags("Projects")
                .Produces<List<ProjectDto>>(StatusCodes.Status200OK);

                endpoints.MapGet("/api/projects/{projectCode}", async (
                    string projectCode,
                    IProjectTransformService projectService,
                    ICurrentTenant currentTenant) =>
                {
                    if (currentTenant.Id == null)
                    {
                        return Results.NotFound();
                    }
                    var project = await projectService.GetProjectDetailAsync(projectCode);
                    if (project == null)
                    {
                        return Results.NotFound();
                    }
                    return Results.Ok(project);
                })
                .WithName("GetProjectDetail")
                .WithTags("Projects")
                .Produces<ProjectDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

                endpoints.MapGet("/api/settings/stages", async (
                    IStageSettingsService stageService) =>
                {
                    var stages = await stageService.GetStagesAsync();
                    return Results.Ok(stages);
                })
                .WithName("GetStageSettings")
                .WithTags("Settings")
                .Produces<List<StageConfigItem>>(StatusCodes.Status200OK);

                endpoints.MapPut("/api/settings/stages", async (
                    List<StageConfigItem> stages,
                    IStageSettingsService stageService) =>
                {
                    var updated = await stageService.SaveStagesAsync(stages);
                    return Results.Ok(updated);
                })
                .WithName("UpdateStageSettings")
                .WithTags("Settings")
                .Produces<List<StageConfigItem>>(StatusCodes.Status200OK);

                endpoints.MapGet("/api/user-preference", async (
                    IUserPreferenceService preferenceService,
                    ICurrentUser currentUser,
                    HttpContext httpContext) =>
                {
                    var userId = currentUser.Id?.ToString() 
                        ?? httpContext.Request.Query["userId"].FirstOrDefault() 
                        ?? currentUser.UserName 
                        ?? "default-user";
                    var pref = await preferenceService.GetAsync(userId);
                    return Results.Ok(pref);
                })
                .WithName("GetUserPreference")
                .WithTags("UserPreference")
                .Produces<UserUiPreferenceDto>(StatusCodes.Status200OK);

endpoints.MapPut("/api/user-preference", async (
                    UpdateUserUiPreferenceInput input,
                    IUserPreferenceService preferenceService,
                    ICurrentUser currentUser,
                    HttpContext httpContext) =>
                {
                    var userId = currentUser.Id?.ToString() 
                        ?? httpContext.Request.Query["userId"].FirstOrDefault() 
                        ?? currentUser.UserName 
                        ?? "default-user";
                    var updated = await preferenceService.SetAsync(userId, input);
                    return Results.Ok(updated);
                })
                .WithName("UpdateUserPreference")
                .WithTags("UserPreference")
                .Produces<UserUiPreferenceDto>(StatusCodes.Status200OK);

                endpoints.MapGet("/api/deep-links/context", async (
                    string projectCode,
                    string? stageCode,
                    string? unitCode,
                    string? itemType,
                    string? itemCode,
                    IDeepLinkService deepLinkService) =>
                {
                    var result = await deepLinkService.GetContextAsync(projectCode, stageCode, unitCode, itemType, itemCode);
                    if (!result.HasPermission)
                    {
                        return Results.Json(result, statusCode: StatusCodes.Status403Forbidden);
                    }
                    return Results.Ok(result);
                })
                .WithName("GetDeepLinkContext")
                .WithTags("DeepLinks")
                .Produces<DeepLinkContextResponse>(StatusCodes.Status200OK)
                .Produces<DeepLinkContextResponse>(StatusCodes.Status403Forbidden);
            });

            try
            {
                using var scope = context.ServiceProvider.CreateScope();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var connStr = configuration.GetConnectionString("Default");
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    var dbContext = scope.ServiceProvider.GetService<NpiDbContext>();
                    dbContext?.Database.EnsureCreated();

                    var identityDbContext = scope.ServiceProvider.GetService<NpiIdentityDbContext>();
                    if (identityDbContext != null)
                    {
                        var creator = (Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator)
                            Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>(identityDbContext.Database);
                        try
                        {
                            creator.CreateTables();
                        }
                        catch
                        {
                            // Ignore if tables already exist
                        }
                    }

                    var dataSeeder = scope.ServiceProvider.GetService<IDataSeeder>();
                    dataSeeder?.SeedAsync().GetAwaiter().GetResult();
                }
            }
            catch
            {
                // Ignore DB initialization errors on startup
            }
        }
    }
}

public partial class Program
{
}

public class AccountSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            new Volo.Abp.Settings.SettingDefinition(
                "Abp.Account.EnableLocalLogin",
                "True",
                isVisibleToClients: true
            )
        );
    }
}

public record PingResponse(string Message, DateTime Timestamp);
public record EnabledModulesResponse(IReadOnlyList<string> EnabledModules);
public record UpdateModuleStateRequest(bool IsEnabled);
public record DemoProjectsResponse(IReadOnlyList<string> Projects);


