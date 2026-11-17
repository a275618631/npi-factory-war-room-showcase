using Core.Data;
using Core.Import;

namespace Core.Org;

public class OrgTreeNodeDto
{
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string? ParentCode { get; set; }
    public string? Team { get; set; }
    public string? ManagerPersonCode { get; set; }
    public int MemberCount { get; set; }
    public List<OrgTreeNodeDto> Children { get; set; } = new();
}

public class OrgTransformResult
{
    public int OrgUnitsAdded { get; set; }
    public int OrgUnitsUpdated { get; set; }
    public int PersonsAdded { get; set; }
    public int PersonsUpdated { get; set; }
    public List<ImportError> Errors { get; set; } = new();
}


