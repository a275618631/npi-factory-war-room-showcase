namespace Core.Security;

/// <summary>
/// 系統權限定義，命名規則採 Module.Feature.Action
/// </summary>
public static class AppPermissions
{
    public static class Org
    {
        public const string DepartmentRead = "Org.Department.Read";
        public const string DepartmentWrite = "Org.Department.Write";
        public const string PersonRead = "Org.Person.Read";
        public const string PersonWrite = "Org.Person.Write";
    }

    public static class Import
    {
        public const string BatchRead = "Import.Batch.Read";
        public const string BatchCreate = "Import.Batch.Create";
        public const string MappingRead = "Import.Mapping.Read";
        public const string MappingWrite = "Import.Mapping.Write";
    }

    public static class Project
    {
        public const string OverviewRead = "Project.Overview.Read";
        public const string GeneralWrite = "Project.General.Write";
        public const string ScheduleWrite = "Project.Schedule.Write";
    }

    public static class Gate
    {
        public const string ReviewRead = "Gate.Review.Read";
        public const string ReviewSign = "Gate.Review.Sign";
        public const string DeliverableWrite = "Gate.Deliverable.Write";
    }

    /// <summary>
    /// 取得指定角色預設擁有的權限清單
    /// </summary>
    public static IReadOnlySet<string> GetDefaultPermissionsForRole(string role)
    {
        if (string.Equals(role, DefaultRoles.CompanyAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Org.DepartmentRead, Org.DepartmentWrite,
                Org.PersonRead, Org.PersonWrite,
                Import.BatchRead, Import.BatchCreate,
                Import.MappingRead, Import.MappingWrite,
                Project.OverviewRead, Project.GeneralWrite, Project.ScheduleWrite,
                Gate.ReviewRead, Gate.ReviewSign, Gate.DeliverableWrite
            };
        }

        if (string.Equals(role, DefaultRoles.ProjectManager, StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Org.DepartmentRead, Org.PersonRead,
                Project.OverviewRead, Project.GeneralWrite, Project.ScheduleWrite,
                Gate.ReviewRead, Gate.ReviewSign, Gate.DeliverableWrite
            };
        }

        if (string.Equals(role, DefaultRoles.DepartmentMember, StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Org.DepartmentRead, Org.PersonRead,
                Project.OverviewRead,
                Gate.ReviewRead,
                Gate.DeliverableWrite // 需經過部門範圍檢查
            };
        }

        if (string.Equals(role, DefaultRoles.Supervisor, StringComparison.OrdinalIgnoreCase))
        {
            // 主管僅有讀取權限，無任何 Write / Create / Sign 權限
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Org.DepartmentRead,
                Org.PersonRead,
                Project.OverviewRead,
                Gate.ReviewRead
            };
        }

        if (string.Equals(role, DefaultRoles.DataImporter, StringComparison.OrdinalIgnoreCase))
        {
            // 資料匯入帳號只能呼叫匯入 API
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Import.BatchRead,
                Import.BatchCreate
            };
        }

        if (string.Equals(role, DefaultRoles.ExternalPartner, StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Project.OverviewRead
            };
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}


