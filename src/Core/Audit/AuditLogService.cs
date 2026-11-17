using Core.Data;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Core.Audit;

public class AuditLogService : IAuditLogService, ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public AuditLogService(
        NpiDbContext dbContext,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task RecordAsync(
        string category,
        string action,
        string target,
        string? oldValue,
        string? newValue,
        string? operatorId = null,
        string? operatorName = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.Id;
        var opId = !string.IsNullOrWhiteSpace(operatorId)
            ? operatorId
            : (_currentUser.Id?.ToString() ?? "System");
        var opName = !string.IsNullOrWhiteSpace(operatorName)
            ? operatorName
            : (_currentUser.UserName ?? "系統管理者");

        var entry = new AuditLogEntry(
            Guid.NewGuid(),
            category: category,
            action: action,
            target: target,
            oldValue: oldValue,
            newValue: newValue,
            operatorId: opId,
            operatorName: opName,
            tenantId: tenantId,
            details: details);

        await _dbContext.AuditLogs.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogDto>> QueryAsync(
        string? category = null,
        string? target = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.Id;
        var query = _dbContext.AuditLogs.AsNoTracking();

        if (tenantId != null)
        {
            query = query.Where(a => a.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(a => a.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(target))
        {
            query = query.Where(a => a.Target == target);
        }

        var list = await query
            .OrderByDescending(a => a.Timestamp)
            .Take(take)
            .Select(a => new AuditLogDto(
                a.Id,
                a.TenantId,
                a.Category,
                a.Action,
                a.Target,
                a.OldValue,
                a.NewValue,
                a.OperatorId,
                a.OperatorName,
                a.Timestamp,
                a.Details))
            .ToListAsync(cancellationToken);

        return list;
    }
}


