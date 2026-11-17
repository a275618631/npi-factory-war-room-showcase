namespace Core.Security;

/// <summary>
/// 系統 7 個預設角色（依照 docs/ARCHITECTURE.md 規範）
/// </summary>
public static class DefaultRoles
{
    /// <summary>
    /// 平台管理員：開新公司、管理平台設定和模組版本
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";

    /// <summary>
    /// 公司管理員：管理自己公司的設定、模組、人員、角色、匯入
    /// </summary>
    public const string CompanyAdmin = "CompanyAdmin";

    /// <summary>
    /// 專案經理（PM）：管理專案、關卡、問題、排程，可以看全部資料
    /// </summary>
    public const string ProjectManager = "ProjectManager";

    /// <summary>
    /// 部門成員：看專案資料，只能改自己部門負責的項目
    /// </summary>
    public const string DepartmentMember = "DepartmentMember";

    /// <summary>
    /// 主管：唯讀，看被授權的專案和報表；所有寫入均被拒絕
    /// </summary>
    public const string Supervisor = "Supervisor";

    /// <summary>
    /// 關卡審查委員：只能簽核被指定的關卡審查
    /// </summary>
    public const string GateReviewer = "GateReviewer";

    /// <summary>
    /// 外部夥伴：只看被授權的專案和指定的資料
    /// </summary>
    public const string ExternalPartner = "ExternalPartner";

    /// <summary>
    /// 資料匯入帳號：只能呼叫匯入 API，不能登入一般畫面
    /// </summary>
    public const string DataImporter = "DataImporter";

    public static readonly IReadOnlyList<string> All = new[]
    {
        PlatformAdmin,
        CompanyAdmin,
        ProjectManager,
        DepartmentMember,
        Supervisor,
        GateReviewer,
        ExternalPartner,
        DataImporter
    };

    public static bool IsReadOnly(string role) =>
        string.Equals(role, Supervisor, StringComparison.OrdinalIgnoreCase);

    public static bool IsImporterOnly(string role) =>
        string.Equals(role, DataImporter, StringComparison.OrdinalIgnoreCase);
}


