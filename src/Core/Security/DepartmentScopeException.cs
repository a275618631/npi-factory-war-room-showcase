using System.Net;
using Volo.Abp;

namespace Core.Security;

/// <summary>
/// 部門範圍權限例外（HTTP 403 Forbidden）
/// </summary>
public class DepartmentScopeException : UserFriendlyException
{
    public HttpStatusCode StatusCode { get; } = HttpStatusCode.Forbidden;
    public string? TargetUnitCode { get; }
    public string? UserUnitCode { get; }

    public DepartmentScopeException(string message, string? targetUnitCode = null, string? userUnitCode = null)
        : base(message, "403")
    {
        TargetUnitCode = targetUnitCode;
        UserUnitCode = userUnitCode;
    }
}


