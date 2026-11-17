using Core.Data;
using Core.Import;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Core.Org;

public interface IOrgTransformService
{
    Task<OrgTransformResult> TransformAsync(Guid? batchId = null, CancellationToken cancellationToken = default);
    Task<List<OrgTreeNodeDto>> GetOrgTreeAsync(CancellationToken cancellationToken = default);
}

public class OrgTransformService : IOrgTransformService, ITransientDependency
{
    private readonly NpiDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public OrgTransformService(NpiDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    public async Task<OrgTransformResult> TransformAsync(Guid? batchId = null, CancellationToken cancellationToken = default)
    {
        var result = new OrgTransformResult();
        var tenantId = _currentTenant.Id;

        // 1. 處理部門 (StgOrgUnit -> OrgUnit)
        var stgOrgUnitsQuery = _dbContext.StgOrgUnits.AsQueryable();
        if (batchId.HasValue)
        {
            stgOrgUnitsQuery = stgOrgUnitsQuery.Where(u => u.ImportBatchId == batchId.Value);
        }

        var stgOrgUnits = await stgOrgUnitsQuery.OrderBy(u => u.CreatedAt).ToListAsync(cancellationToken);
        var existingOrgUnits = await _dbContext.OrgUnits.ToListAsync(cancellationToken);
        var existingOrgUnitMap = existingOrgUnits.ToDictionary(u => u.UnitCode, StringComparer.OrdinalIgnoreCase);
        var processedOrgSourceIds = new HashSet<Guid>(
            existingOrgUnits.Where(u => u.SourceRecordId.HasValue).Select(u => u.SourceRecordId!.Value));

        var updatedOrgUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var addedOrgUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var stg in stgOrgUnits)
        {
            // 若該筆暫存記錄早已轉入正式區，不重複處理
            if (processedOrgSourceIds.Contains(stg.Id))
            {
                continue;
            }

            if (existingOrgUnitMap.TryGetValue(stg.UnitCode, out var existing))
            {
                existing.UnitName = stg.UnitName;
                existing.ParentCode = stg.ParentCode;
                existing.Team = stg.Team;
                existing.ManagerPersonCode = stg.ManagerPersonCode;
                existing.SourceRecordId = stg.Id;
                existing.UpdatedAt = DateTime.UtcNow;

                if (!addedOrgUnits.Contains(existing.UnitCode) && updatedOrgUnits.Add(existing.UnitCode))
                {
                    result.OrgUnitsUpdated++;
                }
            }
            else
            {
                var newUnit = new OrgUnit(
                    Guid.NewGuid(),
                    stg.UnitCode,
                    stg.UnitName,
                    stg.ParentCode,
                    stg.Team,
                    stg.ManagerPersonCode,
                    tenantId,
                    stg.Id);
                await _dbContext.OrgUnits.AddAsync(newUnit, cancellationToken);
                existingOrgUnitMap[newUnit.UnitCode] = newUnit;
                if (addedOrgUnits.Add(newUnit.UnitCode))
                {
                    result.OrgUnitsAdded++;
                }
            }
        }

        // 2. 處理人員 (StgPerson -> Person)
        var stgPersonsQuery = _dbContext.StgPersons.AsQueryable();
        if (batchId.HasValue)
        {
            stgPersonsQuery = stgPersonsQuery.Where(p => p.ImportBatchId == batchId.Value);
        }

        var stgPersons = await stgPersonsQuery.OrderBy(p => p.CreatedAt).ToListAsync(cancellationToken);
        var existingPersons = await _dbContext.Persons.ToListAsync(cancellationToken);
        var existingPersonMap = existingPersons.ToDictionary(p => p.PersonCode, StringComparer.OrdinalIgnoreCase);
        var processedPersonSourceIds = new HashSet<Guid>(
            existingPersons.Where(p => p.SourceRecordId.HasValue).Select(p => p.SourceRecordId!.Value));

        var updatedPersons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var addedPersons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 有效部門清單（包含已存在與本次新增）
        var validUnitCodes = new HashSet<string>(existingOrgUnitMap.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var stg in stgPersons)
        {
            // 若該筆暫存記錄早已轉入正式區，不重複處理
            if (processedPersonSourceIds.Contains(stg.Id))
            {
                continue;
            }

            // 驗收條件 3：人員的 unit_code 不存在時，該列進錯誤報表，不寫入正式區
            if (!validUnitCodes.Contains(stg.UnitCode))
            {
                var error = new ImportError(
                    Guid.NewGuid(),
                    stg.ImportBatchId,
                    0,
                    "unit_code",
                    stg.UnitCode,
                    $"人員「{stg.DisplayName}」({stg.PersonCode}) 所屬部門代碼「{stg.UnitCode}」不存在",
                    "請確認部門代碼或先建立對應部門",
                    tenantId);
                await _dbContext.ImportErrors.AddAsync(error, cancellationToken);
                result.Errors.Add(error);
                continue;
            }

            // 驗收條件 2：同一個人員再匯入一次（名稱改了）只會更新，不會多一筆
            if (existingPersonMap.TryGetValue(stg.PersonCode, out var existing))
            {
                existing.DisplayName = stg.DisplayName;
                existing.UnitCode = stg.UnitCode;
                existing.Email = stg.Email;
                existing.Title = stg.Title;
                existing.PhoneExt = stg.PhoneExt;
                existing.SiteCode = stg.SiteCode;
                existing.RoleTags = stg.RoleTags;
                existing.SourceRecordId = stg.Id;
                existing.UpdatedAt = DateTime.UtcNow;

                if (!addedPersons.Contains(existing.PersonCode) && updatedPersons.Add(existing.PersonCode))
                {
                    result.PersonsUpdated++;
                }
            }
            else
            {
                var newPerson = new Person(
                    Guid.NewGuid(),
                    stg.PersonCode,
                    stg.DisplayName,
                    stg.UnitCode,
                    tenantId,
                    stg.Id)
                {
                    Email = stg.Email,
                    Title = stg.Title,
                    PhoneExt = stg.PhoneExt,
                    SiteCode = stg.SiteCode,
                    RoleTags = stg.RoleTags
                };
                await _dbContext.Persons.AddAsync(newPerson, cancellationToken);
                existingPersonMap[newPerson.PersonCode] = newPerson;
                if (addedPersons.Add(newPerson.PersonCode))
                {
                    result.PersonsAdded++;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<List<OrgTreeNodeDto>> GetOrgTreeAsync(CancellationToken cancellationToken = default)
    {
        var orgUnits = await _dbContext.OrgUnits.ToListAsync(cancellationToken);
        var persons = await _dbContext.Persons.ToListAsync(cancellationToken);

        var memberCountMap = persons
            .GroupBy(p => p.UnitCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var nodeMap = orgUnits.ToDictionary(
            u => u.UnitCode,
            u => new OrgTreeNodeDto
            {
                UnitCode = u.UnitCode,
                UnitName = u.UnitName,
                ParentCode = u.ParentCode,
                Team = u.Team,
                ManagerPersonCode = u.ManagerPersonCode,
                MemberCount = memberCountMap.GetValueOrDefault(u.UnitCode, 0),
                Children = new List<OrgTreeNodeDto>()
            },
            StringComparer.OrdinalIgnoreCase);

        var rootNodes = new List<OrgTreeNodeDto>();
        foreach (var node in nodeMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(node.ParentCode) && nodeMap.TryGetValue(node.ParentCode, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
            else
            {
                rootNodes.Add(node);
            }
        }

        return rootNodes;
    }
}


