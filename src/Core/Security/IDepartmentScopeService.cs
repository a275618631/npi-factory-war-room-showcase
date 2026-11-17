namespace Core.Security;

public record UserSecurityContext(
    string UserName,
    string Role,
    string? UnitCode = null);

public interface IDepartmentScopeService
{
    /// <summary>
    /// 檢查使用者是否有權限存取/修改指定部門負責的項目
    /// </summary>
    bool CanAccessDepartment(UserSecurityContext user, string targetUnitCode, string action = "Write");

    /// <summary>
    /// 檢查部門範圍權限，若不符合則拋出 DepartmentScopeException (403 Forbidden)
    /// </summary>
    void CheckDepartmentScope(UserSecurityContext user, string targetUnitCode, string action = "Write");

    /// <summary>
    /// 檢查使用者是否有權限執行指定權限項目 (Module.Feature.Action)
    /// </summary>
    bool HasPermission(UserSecurityContext user, string permission);

    /// <summary>
    /// 檢查權限項目，若無此權限則拋出 DepartmentScopeException (403 Forbidden)
    /// </summary>
    void CheckPermission(UserSecurityContext user, string permission);
}


