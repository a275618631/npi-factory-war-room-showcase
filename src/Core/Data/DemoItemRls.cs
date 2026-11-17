using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Core.Data;

public static class DemoItemRls
{
    public const string EnableRlsSql = """
        ALTER TABLE demo_items ENABLE ROW LEVEL SECURITY;
        ALTER TABLE demo_items FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS demo_items_tenant_isolation ON demo_items;
        CREATE POLICY demo_items_tenant_isolation ON demo_items
            FOR ALL
            USING ("TenantId"::text = current_setting('app.tenant_id', true))
            WITH CHECK ("TenantId"::text = current_setting('app.tenant_id', true));
        """;

    public static async Task ApplyRlsAsync(DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        await database.ExecuteSqlRawAsync(EnableRlsSql, cancellationToken);
    }

    public static void ApplyRls(DatabaseFacade database)
    {
        database.ExecuteSqlRaw(EnableRlsSql);
    }
}


