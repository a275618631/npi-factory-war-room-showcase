using System.Globalization;
using System.Text;
using Bogus;
using Core.Import;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace MockData;

public class DvtScenarioGenerator
{
    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        NewLine = "\n",
        HasHeaderRecord = true,
    };

    public static MockDataSet Generate(ScenarioOptions options)
    {
        Randomizer.Seed = new Random(options.Seed);
        var faker = new Faker("zh_TW");

        var dataSet = new MockDataSet();

        // 1. Departments (8 units)
        var deptDefs = new (string Code, string Name, string? Parent, string Team, string ManagerCode)[]
        {
            ("PM", "專案管理", null, "專案", "E1001"),
            ("EE", "電子設計", "RD", "硬體", "E1002"),
            ("ME", "機構設計", "RD", "硬體", "E1003"),
            ("FW", "韌體工程", "RD", "軟體", "E1004"),
            ("SW", "軟體開發", "RD", "軟體", "E1005"),
            ("SQE", "供應商品質", "QA", "品質", "E1006"),
            ("PQE", "產品品質", "QA", "品質", "E1007"),
            ("TE", "測試工程", "MFG", "製造", "E1008"),
        };

        foreach (var d in deptDefs)
        {
            dataSet.OrgUnits.Add(new StgOrgUnitRow(d.Code, d.Name, d.Parent, d.Team, d.ManagerCode));
        }

        // 2. Persons (30 persons: 8 managers + 22 staff)
        var managerProfiles = new (string Code, string Name, string Unit, string Email, string Title)[]
        {
            ("E1001", "示範管理者 01", "PM", "ming.zhang@example.com", "專案經理"),
            ("E1002", "示範管理者 02", "EE", "dahua.li@example.com", "硬體研發經理"),
            ("E1003", "示範管理者 03", "ME", "zhiqiang.wang@example.com", "機構設計經理"),
            ("E1004", "示範管理者 04", "FW", "jianyu.chen@example.com", "韌體工程經理"),
            ("E1005", "示範管理者 05", "SW", "guanyu.lin@example.com", "軟體研發經理"),
            ("E1006", "示範管理者 06", "SQE", "yating.huang@example.com", "供應商品質經理"),
            ("E1007", "示範管理者 07", "PQE", "zonghan.yang@example.com", "產品品質經理"),
            ("E1008", "示範管理者 08", "TE", "junjie.liu@example.com", "測試工程經理"),
        };

        foreach (var m in managerProfiles)
        {
            dataSet.Persons.Add(new StgPersonRow(m.Code, m.Name, m.Unit, m.Email, m.Title));
        }

        var units = deptDefs.Select(d => d.Code).ToArray();
        for (var i = 9; i <= options.PersonCount; i++)
        {
            var code = $"E{1000 + i}";
            var unit = units[(i - 9) % units.Length];
            var lastName = faker.Name.LastName();
            var firstName = faker.Name.FirstName();
            var email = $"emp{1000 + i}@example.com";
            var title = unit switch
            {
                "PM" => "專案專員",
                "EE" => "硬體工程師",
                "ME" => "機構工程師",
                "FW" => "韌體工程師",
                "SW" => "軟體工程師",
                "SQE" => "SQE工程師",
                "PQE" => "品保工程師",
                "TE" => "測試工程師",
                _ => "工程師"
            };

            dataSet.Persons.Add(new StgPersonRow(code, $"{lastName}{firstName}", unit, email, title));
        }

        // 3. Project (1 project)
        dataSet.Projects.Add(new StgProjectRow(
            "NPI-2601",
            "範例產品 A",
            "E1001",
            "DVT",
            "DVT-2",
            600,
            "客戶 A"
        ));

        // 4. Stages (6 stages)
        var stages = new (string Code, DateOnly PlanStart, DateOnly PlanEnd, DateOnly? ActStart, DateOnly? ActEnd, DateOnly? GateDate, string GateOwner)[]
        {
            ("Concept", new(2026, 4, 1), new(2026, 5, 15), new(2026, 4, 1), new(2026, 5, 15), new(2026, 5, 18), "E1001"),
            ("Feasibility", new(2026, 5, 16), new(2026, 7, 15), new(2026, 5, 18), new(2026, 7, 20), new(2026, 7, 22), "E1001"),
            ("EVT", new(2026, 7, 21), new(2026, 9, 30), new(2026, 7, 23), new(2026, 10, 5), new(2026, 10, 8), "E1002"),
            ("DVT", new(2026, 10, 1), new(2026, 12, 15), new(2026, 10, 9), null, new(2026, 12, 20), "E1001"),
            ("PVT", new(2026, 12, 16), new(2027, 2, 15), null, null, new(2027, 2, 20), "E1008"),
            ("MP", new(2027, 2, 16), new(2027, 4, 30), null, null, new(2027, 5, 5), "E1001")
        };

        foreach (var s in stages)
        {
            dataSet.StagePlans.Add(new StgStagePlanRow(
                "NPI-2601",
                s.Code,
                s.PlanStart,
                s.PlanEnd,
                s.ActStart,
                s.ActEnd,
                s.GateDate,
                s.GateOwner
            ));
        }

        // 5. Deliverables (40 deliverables total, exactly 5 deliberately overdue)
        // Concept: 4 (completed)
        for (var i = 1; i <= 4; i++)
        {
            dataSet.Deliverables.Add(new StgDeliverableRow(
                "NPI-2601", "Concept", $"CON-DELIV-0{i}", $"Concept 階段交付物 0{i}",
                "E1001", "E1001", new(2026, 5, 10), true, "已核准", 100, "PM"
            ));
        }

        // Feasibility: 6 (completed)
        for (var i = 1; i <= 6; i++)
        {
            dataSet.Deliverables.Add(new StgDeliverableRow(
                "NPI-2601", "Feasibility", $"FEA-DELIV-0{i}", $"可行性評估交付物 0{i}",
                "E1002", "E1001", new(2026, 7, 10), true, "已核准", 100, "EE"
            ));
        }

        // EVT: 8 (completed)
        for (var i = 1; i <= 8; i++)
        {
            dataSet.Deliverables.Add(new StgDeliverableRow(
                "NPI-2601", "EVT", $"EVT-DELIV-0{i}", $"EVT 階段交付物 0{i}",
                "E1003", "E1001", new(2026, 9, 25), true, "已核准", 100, "ME"
            ));
        }

        // DVT: 14 total
        // 5 Deliberately OVERDUE (due < BaseDate 2026-11-03, status in progress/not started)
        var overdueDvtDeliverables = new (string Code, string Name, string Owner, DateOnly Due, string Status, int Percent, string Unit)[]
        {
            ("DVT-DELIV-01", "關鍵零件 PPAP 審查報告", "E1006", new(2026, 10, 25), "進行中", 40, "SQE"),
            ("DVT-DELIV-02", "主機板散熱模擬分析", "E1002", new(2026, 10, 28), "進行中", 50, "EE"),
            ("DVT-DELIV-03", "機構第二版射出模具檢討", "E1003", new(2026, 10, 30), "進行中", 30, "ME"),
            ("DVT-DELIV-04", "BLE連線穩定度測試數據", "E1004", new(2026, 10, 31), "未開始", 0, "FW"),
            ("DVT-DELIV-05", "測試治具涵蓋率分析", "E1008", new(2026, 11, 1), "進行中", 60, "TE"),
        };

        foreach (var od in overdueDvtDeliverables)
        {
            dataSet.Deliverables.Add(new StgDeliverableRow(
                "NPI-2601", "DVT", od.Code, od.Name,
                od.Owner, "E1001", od.Due, true, od.Status, od.Percent, od.Unit
            ));
        }

        // DVT 4 completed deliverables
        for (var i = 6; i <= 9; i++)
        {
            dataSet.Deliverables.Add(new StgDeliverableRow(
                "NPI-2601", "DVT", $"DVT-DELIV-0{i}", $"DVT 已完成項目 0{i}",
                "E1005", "E1001", new(2026, 10, 20), true, "已核准", 100, "SW"
            ));
        }

        // DVT 5 in-progress deliverables (due >= BaseDate 2026-11-03)
        var dvtFuture = new (string Code, string Name, string Owner, DateOnly Due, string Unit)[]
        {
            ("DVT-DELIV-10", "DVT 功能雙盲檢測清單", "E1007", new(2026, 11, 15), "PQE"),
            ("DVT-DELIV-11", "機構跌落與衝擊試驗報告", "E1003", new(2026, 11, 20), "ME"),
            ("DVT-DELIV-12", "韌體 OTA 更新相容性報告", "E1004", new(2026, 11, 25), "FW"),
            ("DVT-DELIV-13", "整機高低溫運作測試", "E1008", new(2026, 12, 1), "TE"),
            ("DVT-DELIV-14", "DVT 關卡總結報告初稿", "E1001", new(2026, 12, 10), "PM"),
        };

        foreach (var fd in dvtFuture)
        {
            dataSet.Deliverables.Add(new StgDeliverableRow(
                "NPI-2601", "DVT", fd.Code, fd.Name,
                fd.Owner, "E1001", fd.Due, true, "進行中", 50, fd.Unit
            ));
        }

        // PVT: 5 deliverables (future, not started)
        for (var i = 1; i <= 5; i++)
        {
            dataSet.Deliverables.Add(new StgDeliverableRow(
                "NPI-2601", "PVT", $"PVT-DELIV-0{i}", $"PVT 產線驗證交付物 0{i}",
                "E1008", "E1001", new(2027, 1, 15), true, "未開始", 0, "TE"
            ));
        }

        // MP: 3 deliverables (future, not started)
        for (var i = 1; i <= 3; i++)
        {
            dataSet.Deliverables.Add(new StgDeliverableRow(
                "NPI-2601", "MP", $"MP-DELIV-0{i}", $"量產移交審核 0{i}",
                "E1001", "E1001", new(2027, 3, 20), true, "未開始", 0, "PM"
            ));
        }

        // 6. Blockers (6 blockers)
        // BLK-0001: SQE, DVT, 8 days overdue (> 3 days), escalated to E1001, reason: 等外部
        dataSet.Blockers.Add(new StgBlockerRow(
            "BLK-0001", "NPI-2601", "DVT", "SQE",
            "關鍵IC供應商 PPAP 等級三審查延遲", "交付物", "DVT-DELIV-01",
            "E1006", new(2026, 10, 26), "派 SQE 工程師至供應商現場稽核", new(2026, 11, 6),
            "供應商品質主管", "卡住", "等外部", "E1001", null, null
        ));

        // BLK-0002: EE, DVT, 6 days overdue (> 3 days), reason: 設計問題
        dataSet.Blockers.Add(new StgBlockerRow(
            "BLK-0002", "NPI-2601", "DVT", "EE",
            "主晶片高溫功耗超標，待硬體版更修正", "交付物", "DVT-DELIV-02",
            "E1002", new(2026, 10, 28), "完成電路負載調整並釋出 Rev B", new(2026, 11, 8),
            "晶片原廠FAE支援", "處理中", "設計問題", null, null, null
        ));

        // BLK-0003: ME, DVT, 2 days overdue (<= 3 days), reason: 缺料
        dataSet.Blockers.Add(new StgBlockerRow(
            "BLK-0003", "NPI-2601", "DVT", "ME",
            "模具第二版試模出現縮水痕", "交付物", "DVT-DELIV-03",
            "E1003", new(2026, 11, 1), "調校射出參數並重測", new(2026, 11, 5),
            "模具廠技術員", "處理中", "缺料", null, null, null
        ));

        // BLK-0004: TE, EVT, Resolved (1 resolved), reason: 等外部
        dataSet.Blockers.Add(new StgBlockerRow(
            "BLK-0004", "NPI-2601", "EVT", "TE",
            "治具訊號量測治具接腳氧化接觸不良", "交付物", "EVT-DELIV-01",
            "E1008", new(2026, 9, 15), "更換治具探針並重新校正", new(2026, 9, 20),
            "治具外包商", "已解除", "等外部", null,
            new(2026, 9, 19), "已更換鍍金探針並完成雙盲驗證"
        ));

        // BLK-0005: FW, DVT, 1 day overdue (<= 3 days), reason: 測試失敗
        dataSet.Blockers.Add(new StgBlockerRow(
            "BLK-0005", "NPI-2601", "DVT", "FW",
            "BLE連線逾時中斷率高於5%", "交付物", "DVT-DELIV-04",
            "E1004", new(2026, 11, 2), "抓取封包分析通訊協定重試機制", new(2026, 11, 7),
            "RF量測實驗室排程", "卡住", "測試失敗", null, null, null
        ));

        // BLK-0006: EE, EVT, 2 days overdue (<= 3 days), reason: 設計問題
        dataSet.Blockers.Add(new StgBlockerRow(
            "BLK-0006", "NPI-2601", "EVT", "EE",
            "抗干擾ESD測試未過標準", "交付物", "EVT-DELIV-02",
            "E1002", new(2026, 11, 1), "增加TVS管及地線屏蔽", new(2026, 11, 6),
            "零件樣品到貨", "處理中", "設計問題", null, null, null
        ));

        return dataSet;
    }

    public static void WriteCsvFiles(MockDataSet dataSet, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        WriteCsv(Path.Combine(outputDirectory, "stg_org_unit.csv"), dataSet.OrgUnits);
        WriteCsv(Path.Combine(outputDirectory, "stg_person.csv"), dataSet.Persons);
        WriteCsv(Path.Combine(outputDirectory, "stg_project.csv"), dataSet.Projects);
        WriteCsv(Path.Combine(outputDirectory, "stg_stage_plan.csv"), dataSet.StagePlans);
        WriteCsv(Path.Combine(outputDirectory, "stg_deliverable.csv"), dataSet.Deliverables);
        WriteCsv(Path.Combine(outputDirectory, "stg_blocker.csv"), dataSet.Blockers);
    }

    private static void WriteCsv<T>(string filePath, IEnumerable<T> records)
    {
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(false));
        using var csv = new CsvWriter(writer, CsvConfig);

        csv.Context.TypeConverterOptionsCache.GetOptions<DateOnly>().Formats = new[] { "yyyy-MM-dd" };
        csv.Context.TypeConverterOptionsCache.GetOptions<DateOnly?>().Formats = new[] { "yyyy-MM-dd" };

        var boolOptions = csv.Context.TypeConverterOptionsCache.GetOptions<bool>();
        boolOptions.BooleanTrueValues.Clear();
        boolOptions.BooleanTrueValues.Add("true");
        boolOptions.BooleanFalseValues.Clear();
        boolOptions.BooleanFalseValues.Add("false");

        csv.WriteRecords(records);
    }

    public static async Task<PersonImportResult> ImportPersonsAsync(
        PersonImportService importService,
        string csvPath,
        string? contractPath = null,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(csvPath);
        return await importService.ImportCsvAsync(stream, Path.GetFileName(csvPath), contractPath, cancellationToken);
    }
}



