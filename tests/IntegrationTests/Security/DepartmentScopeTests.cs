using Core.Security;
using System.Net;
using Xunit;

namespace IntegrationTests.Security;

public class DepartmentScopeTests
{
    private readonly IDepartmentScopeService _service = new DepartmentScopeService();

    [Fact]
    [Trait("Category", "")]
    public void DefaultRoles_ContainsAllRequiredSevenRoles()
    {
        Assert.Equal(8, DefaultRoles.All.Count); // 7 個預設角色 + 關卡審查委員
        Assert.Contains(DefaultRoles.PlatformAdmin, DefaultRoles.All);
        Assert.Contains(DefaultRoles.CompanyAdmin, DefaultRoles.All);
        Assert.Contains(DefaultRoles.ProjectManager, DefaultRoles.All);
        Assert.Contains(DefaultRoles.DepartmentMember, DefaultRoles.All);
        Assert.Contains(DefaultRoles.Supervisor, DefaultRoles.All);
        Assert.Contains(DefaultRoles.ExternalPartner, DefaultRoles.All);
        Assert.Contains(DefaultRoles.DataImporter, DefaultRoles.All);
    }

    [Theory]
    [Trait("Category", "")]
    [InlineData(AppPermissions.Org.DepartmentRead)]
    [InlineData(AppPermissions.Org.DepartmentWrite)]
    [InlineData(AppPermissions.Import.BatchCreate)]
    [InlineData(AppPermissions.Project.ScheduleWrite)]
    [InlineData(AppPermissions.Gate.ReviewSign)]
    public void AppPermissions_Follows_ModuleFeatureAction_Format(string permission)
    {
        var parts = permission.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.False(string.IsNullOrWhiteSpace(parts[0]));
        Assert.False(string.IsNullOrWhiteSpace(parts[1]));
        Assert.False(string.IsNullOrWhiteSpace(parts[2]));
    }

    [Fact]
    [Trait("Category", "")]
    public void DepartmentMember_CanModifyOwnDepartmentItem_Successfully()
    {
        var member = new UserSecurityContext("user_ee", DefaultRoles.DepartmentMember, "EE");

        // 改自己部門負責的項目 -> 允許
        Assert.True(_service.CanAccessDepartment(member, "EE", "Write"));
        var exception = Record.Exception(() => _service.CheckDepartmentScope(member, "EE", "Write"));
        Assert.Null(exception);
    }

    [Fact]
    [Trait("Category", "")]
    public void DepartmentMember_ModifyingOtherDepartmentItem_IsRejectedWith403()
    {
        var member = new UserSecurityContext("user_ee", DefaultRoles.DepartmentMember, "EE");

        // 改別的部門（PM 或 ME）負責的項目 -> 拒絕
        Assert.False(_service.CanAccessDepartment(member, "PM", "Write"));

        var ex = Assert.Throws<DepartmentScopeException>(() =>
            _service.CheckDepartmentScope(member, "PM", "Write"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal("403", ex.Code);
        Assert.Contains("無權修改部門「PM」負責之項目", ex.Message);
    }

    [Theory]
    [Trait("Category", "")]
    [InlineData("Write")]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("Sign")]
    public void Supervisor_AllWriteActions_AreRejectedWith403(string writeAction)
    {
        var supervisor = new UserSecurityContext("boss", DefaultRoles.Supervisor, "EE");

        // 主管所有寫入操作均被拒絕
        Assert.False(_service.CanAccessDepartment(supervisor, "EE", writeAction));

        var ex = Assert.Throws<DepartmentScopeException>(() =>
            _service.CheckDepartmentScope(supervisor, "EE", writeAction));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal("403", ex.Code);
        Assert.Contains("主管角色為唯讀，所有寫入操作均被拒絕", ex.Message);
    }

    [Fact]
    [Trait("Category", "")]
    public void Supervisor_ReadAction_IsAllowed()
    {
        var supervisor = new UserSecurityContext("boss", DefaultRoles.Supervisor, "EE");

        // 主管看專案/報表 -> 允許
        Assert.True(_service.CanAccessDepartment(supervisor, "EE", "Read"));
        Assert.True(_service.CanAccessDepartment(supervisor, "PM", "Read"));
    }

    [Fact]
    [Trait("Category", "")]
    public void DataImporter_CanOnlyCallImportApis_BusinessOperationsRejected()
    {
        var importer = new UserSecurityContext("importer_service", DefaultRoles.DataImporter);

        // 呼叫匯入權限 -> 通過
        Assert.True(_service.HasPermission(importer, AppPermissions.Import.BatchCreate));
        Assert.True(_service.HasPermission(importer, AppPermissions.Import.BatchRead));

        // 呼叫非匯入權限（如修改專案、部門） -> 拒絕
        Assert.False(_service.HasPermission(importer, AppPermissions.Org.DepartmentWrite));
        Assert.False(_service.HasPermission(importer, AppPermissions.Project.GeneralWrite));

        var permEx = Assert.Throws<DepartmentScopeException>(() =>
            _service.CheckPermission(importer, AppPermissions.Project.GeneralWrite));
        Assert.Equal(HttpStatusCode.Forbidden, permEx.StatusCode);

        // 存取部門項目 -> 拒絕
        Assert.False(_service.CanAccessDepartment(importer, "EE", "Write"));
        var scopeEx = Assert.Throws<DepartmentScopeException>(() =>
            _service.CheckDepartmentScope(importer, "EE", "Write"));
        Assert.Equal(HttpStatusCode.Forbidden, scopeEx.StatusCode);
        Assert.Contains("資料匯入帳號只能呼叫匯入 API", scopeEx.Message);
    }

    [Theory]
    [Trait("Category", "")]
    [InlineData(DefaultRoles.CompanyAdmin)]
    [InlineData(DefaultRoles.ProjectManager)]
    public void AdminAndPm_CanModifyAnyDepartment_Successfully(string role)
    {
        var manager = new UserSecurityContext("admin_or_pm", role, "PM");

        // PM 和公司管理員可以修改任何部門負責的項目
        Assert.True(_service.CanAccessDepartment(manager, "EE", "Write"));
        Assert.True(_service.CanAccessDepartment(manager, "ME", "Write"));
        Assert.True(_service.CanAccessDepartment(manager, "PQE", "Write"));

        var ex = Record.Exception(() => _service.CheckDepartmentScope(manager, "EE", "Write"));
        Assert.Null(ex);
    }
}


