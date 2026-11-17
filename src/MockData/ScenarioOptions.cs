namespace MockData;

public class ScenarioOptions
{
    public int Seed { get; set; } = 20261103;
    public int CompanyCount { get; set; } = 1;
    public int DepartmentCount { get; set; } = 8;
    public int PersonCount { get; set; } = 30;
    public int ProjectCount { get; set; } = 1;
    public int StageCount { get; set; } = 6;
    public int DeliverableCount { get; set; } = 40;
    public int OverdueDeliverableCount { get; set; } = 5;
    public int BlockerCount { get; set; } = 6;
    public int LongOverdueBlockerCount { get; set; } = 2; // > 3 days
    public DateOnly BaseDate { get; set; } = new(2026, 11, 3);
    public string OutputDirectory { get; set; } = "./mock-data";
    public Guid TenantId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public string TenantCode { get; set; } = "DEMO";
    public string TenantName { get; set; } = "Acme Hardware Corp";
}


