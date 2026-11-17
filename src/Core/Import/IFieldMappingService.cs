namespace Core.Import;

public interface IFieldMappingService
{
    Task<Dictionary<string, string>> GetMappingAsync(string tableName, CancellationToken cancellationToken = default);
    Task SetMappingAsync(string tableName, Dictionary<string, string> mappings, CancellationToken cancellationToken = default);
}


