namespace Core.Projects;

public record StagePlanDto(
    string StageCode,
    string StageName,
    DateOnly PlannedStart,
    DateOnly PlannedEnd,
    DateOnly? ActualStart,
    DateOnly? ActualEnd,
    DateOnly? GateDate,
    string? GateOwnerPersonCode,
    int DelayDays,
    bool IsDelayed,
    bool IsCurrent);

public record ProjectDto(
    string ProjectCode,
    string ProjectName,
    string PmPersonCode,
    string CurrentStageCode,
    string? CurrentBuild,
    int? PlannedQty,
    string? CustomerRef,
    int TotalDelayDays,
    List<StagePlanDto> Stages);

public class ProjectTransformResult
{
    public int ProjectsImported { get; set; }
    public int ProjectsUpdated { get; set; }
    public int StagePlansImported { get; set; }
    public int StagePlansUpdated { get; set; }
    public List<string> Errors { get; set; } = new();
}


