using Core.Audit;
using Core.UserPreference;
using Core.Import;
using Core.Org;
using Core.Projects;
using Core.Security;
using Core.Settings;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Core.Data;

[ConnectionStringName("Default")]
public class NpiDbContext : AbpDbContext<NpiDbContext>
{
    private readonly TenantRlsConnectionInterceptor? _rlsInterceptor;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<DemoItem> DemoItems => Set<DemoItem>();
    public DbSet<TenantFeatureSetting> TenantFeatureSettings => Set<TenantFeatureSetting>();
    public DbSet<StgOrgUnit> StgOrgUnits => Set<StgOrgUnit>();
    public DbSet<StgPerson> StgPersons => Set<StgPerson>();
    public DbSet<StgProject> StgProjects => Set<StgProject>();
    public DbSet<StgStagePlan> StgStagePlans => Set<StgStagePlan>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<StagePlan> StagePlans => Set<StagePlan>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<TenantApiKey> TenantApiKeys => Set<TenantApiKey>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<UserUiPreference> UserUiPreferences => Set<UserUiPreference>();

    public NpiDbContext(
        DbContextOptions<NpiDbContext> options,
        TenantRlsConnectionInterceptor? rlsInterceptor = null)
        : base(options)
    {
        _rlsInterceptor = rlsInterceptor;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        if (_rlsInterceptor != null)
        {
            optionsBuilder.AddInterceptors(_rlsInterceptor);
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(b =>
        {
            b.ToTable("tenants");
            b.ConfigureByConvention();
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).IsRequired().HasMaxLength(128);
            b.Property(t => t.Code).IsRequired().HasMaxLength(64);
            b.HasIndex(t => t.Code).IsUnique();
        });

        builder.Entity<DemoItem>(b =>
        {
            b.ToTable("demo_items");
            b.ConfigureByConvention();
            b.HasKey(d => d.Id);
            b.Property(d => d.Name).IsRequired().HasMaxLength(128);
            b.Property(d => d.TenantId).IsRequired();
            b.HasIndex(d => d.TenantId);
        });

        builder.Entity<TenantFeatureSetting>(b =>
        {
            b.ToTable("tenant_feature_settings");
            b.ConfigureByConvention();
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).IsRequired().HasMaxLength(128);
            b.Property(s => s.Value).IsRequired().HasMaxLength(128);
            b.Property(s => s.TenantId).IsRequired();
            b.HasIndex(s => new { s.TenantId, s.Name }).IsUnique();
        });

        builder.Entity<StgOrgUnit>(b =>
        {
            b.ToTable("stg_org_unit");
            b.ConfigureByConvention();
            b.HasKey(u => u.Id);
            b.Property(u => u.UnitCode).IsRequired().HasMaxLength(64);
            b.Property(u => u.UnitName).IsRequired().HasMaxLength(128);
            b.Property(u => u.ParentCode).HasMaxLength(64);
            b.Property(u => u.Team).HasMaxLength(64);
            b.Property(u => u.ManagerPersonCode).HasMaxLength(64);
            b.Property(u => u.TenantId).IsRequired();
            b.HasIndex(u => u.TenantId);
        });

        builder.Entity<StgPerson>(b =>
        {
            b.ToTable("stg_person");
            b.ConfigureByConvention();
            b.HasKey(p => p.Id);
            b.Property(p => p.PersonCode).IsRequired().HasMaxLength(64);
            b.Property(p => p.DisplayName).IsRequired().HasMaxLength(128);
            b.Property(p => p.UnitCode).IsRequired().HasMaxLength(64);
            b.Property(p => p.Email).HasMaxLength(256);
            b.Property(p => p.Title).HasMaxLength(128);
            b.Property(p => p.PhoneExt).HasMaxLength(64);
            b.Property(p => p.SiteCode).HasMaxLength(64);
            b.Property(p => p.RoleTags).HasMaxLength(256);
            b.Property(p => p.TenantId).IsRequired();
            b.HasIndex(p => p.TenantId);
        });

        builder.Entity<ImportBatch>(b =>
        {
            b.ToTable("import_batch");
            b.ConfigureByConvention();
            b.HasKey(ib => ib.Id);
            b.Property(ib => ib.TableName).IsRequired().HasMaxLength(64);
            b.Property(ib => ib.FileName).IsRequired().HasMaxLength(256);
            b.Property(ib => ib.FileHash).IsRequired().HasMaxLength(64);
            b.Property(ib => ib.Status).IsRequired().HasMaxLength(32);
            b.Property(ib => ib.TenantId).IsRequired();
            b.HasIndex(ib => ib.TenantId);
            b.HasIndex(ib => new { ib.TenantId, ib.FileHash });
        });

        builder.Entity<ImportError>(b =>
        {
            b.ToTable("import_error");
            b.ConfigureByConvention();
            b.HasKey(ie => ie.Id);
            b.Property(ie => ie.FieldName).IsRequired().HasMaxLength(64);
            b.Property(ie => ie.Reason).IsRequired().HasMaxLength(512);
            b.Property(ie => ie.OriginalValue).HasMaxLength(512);
            b.Property(ie => ie.Suggestion).HasMaxLength(512);
            b.Property(ie => ie.TenantId).IsRequired();
            b.HasIndex(ie => ie.TenantId);
            b.HasIndex(ie => ie.ImportBatchId);
        });

        builder.Entity<StgProject>(b =>
        {
            b.ToTable("stg_project");
            b.ConfigureByConvention();
            b.HasKey(p => p.Id);
            b.Property(p => p.ProjectCode).IsRequired().HasMaxLength(64);
            b.Property(p => p.ProjectName).IsRequired().HasMaxLength(128);
            b.Property(p => p.PmPersonCode).IsRequired().HasMaxLength(64);
            b.Property(p => p.CurrentStageCode).IsRequired().HasMaxLength(64);
            b.Property(p => p.CurrentBuild).HasMaxLength(64);
            b.Property(p => p.CustomerRef).HasMaxLength(128);
            b.Property(p => p.TenantId).IsRequired();
            b.HasIndex(p => p.TenantId);
        });

        builder.Entity<StgStagePlan>(b =>
        {
            b.ToTable("stg_stage_plan");
            b.ConfigureByConvention();
            b.HasKey(s => s.Id);
            b.Property(s => s.ProjectCode).IsRequired().HasMaxLength(64);
            b.Property(s => s.StageCode).IsRequired().HasMaxLength(64);
            b.Property(s => s.GateOwnerPersonCode).HasMaxLength(64);
            b.Property(s => s.TenantId).IsRequired();
            b.HasIndex(s => s.TenantId);
        });

        builder.Entity<OrgUnit>(b =>
        {
            b.ToTable("org_unit");
            b.ConfigureByConvention();
            b.HasKey(u => u.Id);
            b.Property(u => u.UnitCode).IsRequired().HasMaxLength(64);
            b.Property(u => u.UnitName).IsRequired().HasMaxLength(128);
            b.Property(u => u.ParentCode).HasMaxLength(64);
            b.Property(u => u.Team).HasMaxLength(64);
            b.Property(u => u.ManagerPersonCode).HasMaxLength(64);
            b.Property(u => u.TenantId).IsRequired();
            b.HasIndex(u => u.TenantId);
            b.HasIndex(u => new { u.TenantId, u.UnitCode }).IsUnique();
        });

        builder.Entity<Person>(b =>
        {
            b.ToTable("person");
            b.ConfigureByConvention();
            b.HasKey(p => p.Id);
            b.Property(p => p.PersonCode).IsRequired().HasMaxLength(64);
            b.Property(p => p.DisplayName).IsRequired().HasMaxLength(128);
            b.Property(p => p.UnitCode).IsRequired().HasMaxLength(64);
            b.Property(p => p.Email).HasMaxLength(256);
            b.Property(p => p.Title).HasMaxLength(128);
            b.Property(p => p.PhoneExt).HasMaxLength(64);
            b.Property(p => p.SiteCode).HasMaxLength(64);
            b.Property(p => p.RoleTags).HasMaxLength(256);
            b.Property(p => p.TenantId).IsRequired();
            b.HasIndex(p => p.TenantId);
            b.HasIndex(p => new { p.TenantId, p.PersonCode }).IsUnique();
        });

        builder.Entity<Project>(b =>
        {
            b.ToTable("projects");
            b.ConfigureByConvention();
            b.HasKey(p => p.Id);
            b.Property(p => p.ProjectCode).IsRequired().HasMaxLength(64);
            b.Property(p => p.ProjectName).IsRequired().HasMaxLength(128);
            b.Property(p => p.PmPersonCode).IsRequired().HasMaxLength(64);
            b.Property(p => p.CurrentStageCode).IsRequired().HasMaxLength(64);
            b.Property(p => p.CurrentBuild).HasMaxLength(64);
            b.Property(p => p.CustomerRef).HasMaxLength(128);
            b.Property(p => p.TenantId).IsRequired();
            b.HasIndex(p => p.TenantId);
            b.HasIndex(p => new { p.TenantId, p.ProjectCode }).IsUnique();
        });

        builder.Entity<StagePlan>(b =>
        {
            b.ToTable("stage_plans");
            b.ConfigureByConvention();
            b.HasKey(s => s.Id);
            b.Property(s => s.ProjectCode).IsRequired().HasMaxLength(64);
            b.Property(s => s.StageCode).IsRequired().HasMaxLength(64);
            b.Property(s => s.GateOwnerPersonCode).HasMaxLength(64);
            b.Property(s => s.TenantId).IsRequired();
            b.HasIndex(s => s.TenantId);
            b.HasIndex(s => new { s.TenantId, s.ProjectCode, s.StageCode }).IsUnique();
        });

        builder.Entity<AppSetting>(b =>
        {
            b.ToTable("app_settings");
            b.ConfigureByConvention();
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).IsRequired().HasMaxLength(128);
            b.Property(s => s.Value).IsRequired().HasMaxLength(2048);
            b.Property(s => s.TenantId).IsRequired();
            b.HasIndex(s => s.TenantId);
            b.HasIndex(s => new { s.TenantId, s.Name }).IsUnique();
        });

        builder.Entity<TenantApiKey>(b =>
        {
            b.ToTable("tenant_api_keys");
            b.ConfigureByConvention();
            b.HasKey(k => k.Id);
            b.Property(k => k.Key).IsRequired().HasMaxLength(64);
            b.Property(k => k.Name).IsRequired().HasMaxLength(128);
            b.Property(k => k.TenantId).IsRequired();
            b.HasIndex(k => k.TenantId);
            b.HasIndex(k => k.Key).IsUnique();
        });

        builder.Entity<AuditLogEntry>(b =>
        {
            b.ToTable("audit_logs");
            b.ConfigureByConvention();
            b.HasKey(a => a.Id);
            b.Property(a => a.Category).IsRequired().HasMaxLength(64);
            b.Property(a => a.Action).IsRequired().HasMaxLength(64);
            b.Property(a => a.Target).IsRequired().HasMaxLength(128);
            b.Property(a => a.OperatorId).IsRequired().HasMaxLength(64);
            b.Property(a => a.OperatorName).IsRequired().HasMaxLength(128);
            b.Property(a => a.OldValue).HasMaxLength(4000);
            b.Property(a => a.NewValue).HasMaxLength(4000);
            b.Property(a => a.Details).HasMaxLength(4000);
            b.HasIndex(a => a.TenantId);
            b.HasIndex(a => a.Timestamp);
        });

        builder.Entity<UserUiPreference>(b =>
        {
            b.ToTable("user_ui_preference");
            b.ConfigureByConvention();
            b.HasKey(u => u.Id);
            b.Property(u => u.UserId).IsRequired().HasMaxLength(128);
            b.Property(u => u.ViewMode).HasMaxLength(32);
            b.Property(u => u.Motion).HasMaxLength(32);
            b.Property(u => u.LastProjectCode).HasMaxLength(64);
            b.HasIndex(u => u.TenantId);
            b.HasIndex(u => new { u.TenantId, u.UserId }).IsUnique();
        });
    }
}


