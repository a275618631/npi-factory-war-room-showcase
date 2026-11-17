using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Volo.Abp.MultiTenancy;

namespace Core.Data;

public class TenantRlsConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ICurrentTenant _currentTenant;

    public TenantRlsConnectionInterceptor(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetTenantSessionVariable(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetTenantSessionVariableAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public void SetTenantSessionVariable(DbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            return;
        }

        var tenantId = _currentTenant.Id?.ToString() ?? string.Empty;
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenantId, false);";
        var param = command.CreateParameter();
        param.ParameterName = "tenantId";
        param.Value = tenantId;
        command.Parameters.Add(param);
        command.ExecuteNonQuery();
    }

    public async Task SetTenantSessionVariableAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open)
        {
            return;
        }

        var tenantId = _currentTenant.Id?.ToString() ?? string.Empty;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenantId, false);";
        var param = command.CreateParameter();
        param.ParameterName = "tenantId";
        param.Value = tenantId;
        command.Parameters.Add(param);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}


