using Volo.Abp.DependencyInjection;

namespace Core.Security;

public class DepartmentScopeService : IDepartmentScopeService, ITransientDependency
{
    private static readonly HashSet<string> WriteActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Write", "Create", "Update", "Delete", "Sign", "Edit", "Modify"
    };

    public bool CanAccessDepartment(UserSecurityContext user, string targetUnitCode, string action = "Write")
    {
        if (user == null)
        {
            return false;
        }

        var isWriteAction = WriteActions.Contains(action);

        // 1. 資料匯入帳號只能呼叫匯入 API，不能修改或存取部門項目
        if (DefaultRoles.IsImporterOnly(user.Role))
        {
            return false;
        }

        // 2. 主管角色唯讀：看專案/報表可以，但所有寫入操作均被拒絕
        if (DefaultRoles.IsReadOnly(user.Role))
        {
            return !isWriteAction;
        }

        // 3. 平台管理員、公司管理員、專案經理（PM）：具備全域部門管理權限
        if (string.Equals(user.Role, DefaultRoles.CompanyAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Role, DefaultRoles.ProjectManager, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Role, DefaultRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 4. 外部夥伴：僅允許唯讀，禁止任何寫入
        if (string.Equals(user.Role, DefaultRoles.ExternalPartner, StringComparison.OrdinalIgnoreCase))
        {
            return !isWriteAction;
        }

        // 5. 部門成員：看資料可以；寫入時「只能改自己部門負責的項目」
        if (string.Equals(user.Role, DefaultRoles.DepartmentMember, StringComparison.OrdinalIgnoreCase))
        {
            if (!isWriteAction)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(user.UnitCode))
            {
                return false;
            }

            return string.Equals(user.UnitCode.Trim(), targetUnitCode?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public void CheckDepartmentScope(UserSecurityContext user, string targetUnitCode, string action = "Write")
    {
        if (CanAccessDepartment(user, targetUnitCode, action))
        {
            return;
        }

        if (user != null && DefaultRoles.IsImporterOnly(user.Role))
        {
            throw new DepartmentScopeException(
                "資料匯入帳號只能呼叫匯入 API，無法執行部門業務操作",
                targetUnitCode,
                user.UnitCode);
        }

        if (user != null && DefaultRoles.IsReadOnly(user.Role))
        {
            throw new DepartmentScopeException(
                $"主管角色為唯讀，所有寫入操作均被拒絕",
                targetUnitCode,
                user.UnitCode);
        }

        if (user != null && string.Equals(user.Role, DefaultRoles.DepartmentMember, StringComparison.OrdinalIgnoreCase))
        {
            throw new DepartmentScopeException(
                $"部門成員所屬部門「{user.UnitCode}」無權修改部門「{targetUnitCode}」負責之項目",
                targetUnitCode,
                user.UnitCode);
        }

        throw new DepartmentScopeException(
            $"使用者「{user?.UserName}」無權對部門「{targetUnitCode}」執行「{action}」操作",
            targetUnitCode,
            user?.UnitCode);
    }

    public bool HasPermission(UserSecurityContext user, string permission)
    {
        if (user == null || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        var allowed = AppPermissions.GetDefaultPermissionsForRole(user.Role);
        return allowed.Contains(permission);
    }

    public void CheckPermission(UserSecurityContext user, string permission)
    {
        if (!HasPermission(user, permission))
        {
            throw new DepartmentScopeException(
                $"角色「{user?.Role}」未被授權執行權限「{permission}」");
        }
    }
}


