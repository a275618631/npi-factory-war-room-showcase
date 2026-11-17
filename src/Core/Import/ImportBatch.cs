using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Core.Import;

public static class ImportBatchStatus
{
    public const string Success = "成功";
    public const string Failed = "失敗";
}

public class ImportBatch : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string Status { get; set; } = ImportBatchStatus.Success;
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ImportBatch()
    {
    }

    public ImportBatch(Guid id, string tableName, string fileName, string fileHash, Guid? tenantId = null)
        : base(id)
    {
        TableName = tableName;
        FileName = fileName;
        FileHash = fileHash;
        TenantId = tenantId;
    }
}


