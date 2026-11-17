namespace MockData;

public record StgOrgUnitRow(
    string unit_code,
    string unit_name,
    string? parent_code,
    string? team,
    string? manager_person_code);

public record StgPersonRow(
    string person_code,
    string display_name,
    string unit_code,
    string? email,
    string? title);

public record StgProjectRow(
    string project_code,
    string project_name,
    string pm_person_code,
    string current_stage_code,
    string? current_build,
    int? planned_qty,
    string? customer_ref);

public record StgStagePlanRow(
    string project_code,
    string stage_code,
    DateOnly planned_start,
    DateOnly planned_end,
    DateOnly? actual_start,
    DateOnly? actual_end,
    DateOnly? gate_date,
    string? gate_owner_person_code);

public record StgDeliverableRow(
    string project_code,
    string stage_code,
    string deliverable_code,
    string deliverable_name,
    string owner_person_code,
    string? receiver_person_code,
    DateOnly due_date,
    bool mandatory,
    string status,
    int? percent_done,
    string? unit_code);

public record StgBlockerRow(
    string blocker_no,
    string project_code,
    string stage_code,
    string unit_code,
    string detail,
    string? ref_type,
    string? ref_code,
    string owner_person_code,
    DateOnly since_date,
    string next_step,
    DateOnly next_step_due,
    string? waiting_for,
    string status,
    string? reason_category,
    string? escalated_to_person_code,
    DateOnly? resolved_date,
    string? resolution_note);

public class MockDataSet
{
    public List<StgOrgUnitRow> OrgUnits { get; set; } = new();
    public List<StgPersonRow> Persons { get; set; } = new();
    public List<StgProjectRow> Projects { get; set; } = new();
    public List<StgStagePlanRow> StagePlans { get; set; } = new();
    public List<StgDeliverableRow> Deliverables { get; set; } = new();
    public List<StgBlockerRow> Blockers { get; set; } = new();
}


