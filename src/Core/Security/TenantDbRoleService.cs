using Core.Data;
using Core.Settings;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Core.Security;

public record TenantDbRoleResult(bool Success, string RoleName, string Message);

public interface ITenantDbRoleService
{
    string GetRoleName(string tenantCode);
    Task<bool> IsDirectDbAccessEnabledAsync(CancellationToken cancellationToken = default);
    Task<TenantDbRoleResult> CreateTenantImporterRoleAsync(
        Guid tenantId,
        string tenantCode,
        string password,
        CancellationToken cancellationToken = default);
    Task<bool> DropTenantImporterRoleAsync(string tenantCode, CancellationToken cancellationToken = default);
}

public class TenantDbRoleService : ITenantDbRoleService, ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly ISettingCenterService _settingCenterService;
    private readonly ICurrentTenant _currentTenant;

    public TenantDbRoleService(
        NpiDbContext dbContext,
        ISettingCenterService settingCenterService,
        ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _settingCenterService = settingCenterService;
        _currentTenant = currentTenant;
    }

    public string GetRoleName(string tenantCode)
    {
        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            throw new ArgumentException("Tenant code cannot be empty.", nameof(tenantCode));
        }

        var clean = Regex.Replace(tenantCode.Trim().ToLowerInvariant(), @"[^a-z0-9_]", "_");
        return $"importer_{clean}";
    }

    public async Task<bool> IsDirectDbAccessEnabledAsync(CancellationToken cancellationToken = default)
    {
        var val = await _settingCenterService.GetValueAsync(SettingDefinitions.Keys.DirectDbAccess, cancellationToken);
        return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "1", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<TenantDbRoleResult> CreateTenantImporterRoleAsync(
        Guid tenantId,
        string tenantCode,
        string password,
        CancellationToken cancellationToken = default)
    {
        var roleName = GetRoleName(tenantCode);

        // 1. 設定關閉時不建立資料庫角色
        var isEnabled = await IsDirectDbAccessEnabledAsync(cancellationToken);
        if (!isEnabled)
        {
            return new TenantDbRoleResult(
                Success: false,
                RoleName: roleName,
                Message: "Direct database access is disabled in settings. Role was not created."
            );
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        }

        var escapedPassword = password.Replace("'", "''");

        // 2. 開啟後，該角色只能 INSERT 自己公司的匯入表，不能讀正式區
        var sql = $"""
            DO $$
            DECLARE
                r RECORD;
                col_name text;
                pol_name text;
            BEGIN
                -- 建立登入角色（非超級使用者、無建庫建角權限、受 RLS 約束）
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{roleName}') THEN
                    CREATE ROLE {roleName} WITH LOGIN PASSWORD '{escapedPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                ELSE
                    ALTER ROLE {roleName} WITH LOGIN PASSWORD '{escapedPassword}';
                END IF;

                -- 允許連線與存取 public schema
                GRANT USAGE ON SCHEMA public TO {roleName};

                -- 嚴格禁止正式區所有資料表的所有存取權限
                REVOKE ALL ON ALL TABLES IN SCHEMA public FROM {roleName};

                -- 針對匯入區 stg_* 表，僅開放 INSERT 權限，並啟用 RLS 限制只能寫入自己租戶
                FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename LIKE 'stg_%') LOOP
                    EXECUTE format('GRANT INSERT ON TABLE public.%I TO %I', r.tablename, '{roleName}');
                    EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', r.tablename);

                    -- 檢測 TenantId 欄位名稱（大小寫自適應：TenantId 或 tenant_id）
                    SELECT column_name INTO col_name 
                    FROM information_schema.columns 
                    WHERE table_schema = 'public' AND table_name = r.tablename 
                      AND lower(column_name) IN ('tenantid', 'tenant_id')
                    LIMIT 1;

                    IF col_name IS NOT NULL THEN
                        pol_name := 'rls_' || r.tablename || '_{roleName}_insert';
                        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', pol_name, r.tablename);
                        EXECUTE format('CREATE POLICY %I ON public.%I FOR INSERT TO %I WITH CHECK (%I = %L)', pol_name, r.tablename, '{roleName}', col_name, '{tenantId}');
                    END IF;
                END LOOP;
            END
            $$;
            """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        return new TenantDbRoleResult(
            Success: true,
            RoleName: roleName,
            Message: $"Successfully created and configured importer role {roleName}."
        );
    }

    public async Task<bool> DropTenantImporterRoleAsync(string tenantCode, CancellationToken cancellationToken = default)
    {
        var roleName = GetRoleName(tenantCode);
        var sql = $"""
            DO $$
            DECLARE
                r RECORD;
                pol_name text;
            BEGIN
                IF EXISTS (SELECT FROM pg_roles WHERE rolname = '{roleName}') THEN
                    -- 清理 RLS 政策
                    FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename LIKE 'stg_%') LOOP
                        pol_name := 'rls_' || r.tablename || '_{roleName}_insert';
                        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', pol_name, r.tablename);
                    END LOOP;

                    REVOKE ALL ON ALL TABLES IN SCHEMA public FROM {roleName};
                    REVOKE USAGE ON SCHEMA public FROM {roleName};
                    DROP ROLE {roleName};
                END IF;
            END
            $$;
            """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        return true;
    }
}


