namespace Core.Settings;

public record SettingDefinition(
    string Key,
    string DisplayName,
    string Category,
    string ValueType,
    string DefaultValue,
    string Description);

public static class SettingCategories
{
    public const string CompanyBasic = "公司基本";
    public const string StageAndGate = "階段與關卡";
    public const string Threshold = "門檻";
    public const string DataImport = "資料匯入";
    public const string ViewMode = "檢視模式";
}

public static class SettingDefinitions
{
    public static class Keys
    {
        // 公司基本
        public const string CompanyName = "Company.Name";
        public const string CompanyLanguage = "Company.Language";
        public const string CompanyTimeZone = "Company.TimeZone";
        public const string CompanyDateFormat = "Company.DateFormat";
        public const string CompanyDataTag = "Company.DataTag";

        // 階段與關卡
        public const string StageTemplate = "Stage.Template";
        public const string StageList = "Stage.List";
        public const string StageDefinitions = "Stage.Definitions";
        public const string GateConditionDeadlineDays = "Gate.ConditionDeadlineDays";
        public const string GateReviewReminderDays = "Gate.ReviewReminderDays";
        public const string GateReviewerCount = "Gate.ReviewerCount";

        // 門檻
        public const string ThresholdTargetFpy = "Threshold.TargetFpy";
        public const string ThresholdAlertFpy = "Threshold.AlertFpy";
        public const string ThresholdYieldDisplay = "Threshold.YieldDisplay";
        public const string ThresholdLongLeadWeeks = "Threshold.LongLeadWeeks";
        public const string ThresholdIssueReminderDays = "Threshold.IssueReminderDays";

        // 資料匯入
        public const string ImportFieldMappingPrefix = "Import.FieldMapping.";
        public const string DirectDbAccess = "DataImport.DirectDbAccess";

        // 檢視模式
        public const string ViewModeDefault = "ViewMode.Default";
        public const string ViewModeAllowSwitch = "ViewMode.AllowSwitch";
        public const string ViewModeAllowMotion = "ViewMode.AllowMotion";
        public const string ViewModeMotionDefault = "ViewMode.MotionDefault";
        public const string ViewModeFollowReducedMotion = "ViewMode.FollowReducedMotion";
        public const string ViewModeNumberCountUp = "ViewMode.NumberCountUp";
    }

    public static readonly IReadOnlyList<SettingDefinition> All = new[]
    {
        // 公司基本
        new SettingDefinition(Keys.CompanyName, "公司名稱", SettingCategories.CompanyBasic, "String", "範例製造股份有限公司", "公司完整名稱"),
        new SettingDefinition(Keys.CompanyLanguage, "介面語言", SettingCategories.CompanyBasic, "String", "zh-TW", "預設顯示語言：zh-TW 或 en-US"),
        new SettingDefinition(Keys.CompanyTimeZone, "時區", SettingCategories.CompanyBasic, "String", "Asia/Taipei", "系統時間顯示時區"),
        new SettingDefinition(Keys.CompanyDateFormat, "日期格式", SettingCategories.CompanyBasic, "String", "YYYY-MM-DD", "介面日期呈現格式"),
        new SettingDefinition(Keys.CompanyDataTag, "資料性質標籤", SettingCategories.CompanyBasic, "String", "測試資料", "資料環境標籤：測試資料或正式資料"),

        // 階段與關卡
        new SettingDefinition(Keys.StageTemplate, "階段範本", SettingCategories.StageAndGate, "String", "硬體六階段", "專案階段預設範本"),
        new SettingDefinition(Keys.StageList, "階段清單", SettingCategories.StageAndGate, "String", "Concept,Feasibility,EVT,DVT,PVT,MP", "階段代碼清單，逗號分隔"),
        new SettingDefinition(Keys.StageDefinitions, "階段詳細設定", SettingCategories.StageAndGate, "Json", "[{\"code\":\"Concept\",\"name\":\"概念\",\"order\":1,\"color\":\"#909399\",\"isActive\":true},{\"code\":\"Feasibility\",\"name\":\"可行性\",\"order\":2,\"color\":\"#E6A23C\",\"isActive\":true},{\"code\":\"EVT\",\"name\":\"工程驗證\",\"order\":3,\"color\":\"#409EFF\",\"isActive\":true},{\"code\":\"DVT\",\"name\":\"設計驗證\",\"order\":4,\"color\":\"#67C23A\",\"isActive\":true},{\"code\":\"PVT\",\"name\":\"生產驗證\",\"order\":5,\"color\":\"#E6A23C\",\"isActive\":true},{\"code\":\"MP\",\"name\":\"量產\",\"order\":6,\"color\":\"#909399\",\"isActive\":true}]", "階段代號、名稱、順序、顏色與啟用狀態設定 JSON"),
        new SettingDefinition(Keys.GateConditionDeadlineDays, "有條件通過補件期限", SettingCategories.StageAndGate, "Number", "14", "關卡有條件通過後補件天數限制"),
        new SettingDefinition(Keys.GateReviewReminderDays, "審查前提醒天數", SettingCategories.StageAndGate, "Number", "7", "關卡審查日前幾天提醒缺件"),
        new SettingDefinition(Keys.GateReviewerCount, "關卡審查人核准人數", SettingCategories.StageAndGate, "Number", "1", "每一關需要幾位審查人核准"),

        // 門檻
        new SettingDefinition(Keys.ThresholdTargetFpy, "工站目標 FPY", SettingCategories.Threshold, "Number", "98", "工站目標一次通過率 (%)"),
        new SettingDefinition(Keys.ThresholdAlertFpy, "工站警戒 FPY", SettingCategories.Threshold, "Number", "95", "工站警戒一次通過率 (%)"),
        new SettingDefinition(Keys.ThresholdYieldDisplay, "整線良率顯示方式", SettingCategories.Threshold, "String", "RTY", "整線良率計算方式：RTY 或 AVG"),
        new SettingDefinition(Keys.ThresholdLongLeadWeeks, "長交期定義週數", SettingCategories.Threshold, "Number", "12", "交期超過幾週視為長交期料件"),
        new SettingDefinition(Keys.ThresholdIssueReminderDays, "問題逾期提醒天數", SettingCategories.Threshold, "Number", "2", "問題到期日前幾天發出提醒"),

        // 資料匯入
        new SettingDefinition(Keys.DirectDbAccess, "直接寫入匯入區資料庫帳號", SettingCategories.DataImport, "Boolean", "false", "是否允許為公司建立只能寫入自己匯入區資料表的專用資料庫帳號，預設關閉"),
        new SettingDefinition(Keys.ImportFieldMappingPrefix + "stg_person", "人員表欄位對應", SettingCategories.DataImport, "Json", "{}", "公司自訂欄位名稱與 stg_person 契約欄位對應 JSON"),

        // 檢視模式
        new SettingDefinition(Keys.ViewModeDefault, "預設檢視模式", SettingCategories.ViewMode, "String", "relaxed", "使用者沒選過時的預設檢視模式：relaxed 或 overview"),
        new SettingDefinition(Keys.ViewModeAllowSwitch, "允許切換檢視模式", SettingCategories.ViewMode, "Boolean", "true", "是否允許使用者切換輕鬆／總覽模式"),
        new SettingDefinition(Keys.ViewModeAllowMotion, "允許動畫", SettingCategories.ViewMode, "Boolean", "true", "公司動畫總開關，關閉後全公司無視個人偏好一律無動畫且個人開關不顯示"),
        new SettingDefinition(Keys.ViewModeMotionDefault, "動畫預設開關", SettingCategories.ViewMode, "Boolean", "true", "使用者沒選過、系統也沒要求減少動態時的動畫預設"),
        new SettingDefinition(Keys.ViewModeFollowReducedMotion, "跟隨系統減少動態", SettingCategories.ViewMode, "Boolean", "true", "是否跟隨作業系統與瀏覽器的 prefers-reduced-motion 設定"),
        new SettingDefinition(Keys.ViewModeNumberCountUp, "數字卡數字跳動動畫", SettingCategories.ViewMode, "Boolean", "false", "輕鬆模式下數字卡是否從 0 跳到實際值，預設關閉")
    };

    private static readonly Dictionary<string, SettingDefinition> Map =
        All.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    public static SettingDefinition? Find(string key) =>
        Map.TryGetValue(key, out var def) ? def : null;
}


