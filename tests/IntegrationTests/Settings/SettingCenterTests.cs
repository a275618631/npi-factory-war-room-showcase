using Core.Settings;
using IntegrationTests.Isolation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Settings;

public class SettingCenterTests : IsolationTestBase
{
    [Fact]
    [Trait("Category", "")]
    public void SettingDefinitions_ContainsRequiredThreeCategories()
    {
        var categories = SettingDefinitions.All.Select(d => d.Category).Distinct().ToList();
        Assert.Contains(SettingCategories.CompanyBasic, categories);
        Assert.Contains(SettingCategories.StageAndGate, categories);
        Assert.Contains(SettingCategories.Threshold, categories);

        Assert.NotNull(SettingDefinitions.Find(SettingDefinitions.Keys.CompanyName));
        Assert.NotNull(SettingDefinitions.Find(SettingDefinitions.Keys.StageTemplate));
        Assert.NotNull(SettingDefinitions.Find(SettingDefinitions.Keys.ThresholdTargetFpy));
    }

    [Fact]
    [Trait("Category", "")]
    public async Task WithoutOverride_ReadsDefaultValues_Successfully()
    {
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var service = sp.GetRequiredService<ISettingCenterService>();

            // 公司基本預設值
            var lang = await service.GetValueAsync(SettingDefinitions.Keys.CompanyLanguage);
            Assert.Equal("zh-TW", lang);

            // 門檻數值預設值（字串與強型別）
            var fpyStr = await service.GetValueAsync(SettingDefinitions.Keys.ThresholdTargetFpy);
            Assert.Equal("98", fpyStr);

            var fpy = await service.GetValueAsync<int>(SettingDefinitions.Keys.ThresholdTargetFpy);
            Assert.Equal(98, fpy);

            var leadWeeks = await service.GetValueAsync<int>(SettingDefinitions.Keys.ThresholdLongLeadWeeks);
            Assert.Equal(12, leadWeeks);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task TenantA_OverrideSetting_OnlyAffectsTenantA_TenantBRemainsDefault()
    {
        // 1. TenantA 覆寫公司名稱與警戒 FPY
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var service = sp.GetRequiredService<ISettingCenterService>();
            await service.SetValueAsync(SettingDefinitions.Keys.CompanyName, "台灣先進電子股份有限公司");
            await service.SetValueAsync(SettingDefinitions.Keys.ThresholdAlertFpy, "90");

            var nameA = await service.GetValueAsync(SettingDefinitions.Keys.CompanyName);
            var alertFpyA = await service.GetValueAsync<int>(SettingDefinitions.Keys.ThresholdAlertFpy);

            Assert.Equal("台灣先進電子股份有限公司", nameA);
            Assert.Equal(90, alertFpyA);
        });

        // 2. TenantB 讀取相同設定項，仍為預設值，完全不受 TenantA 影響
        await ExecuteScopedAsync(TenantBId, async (dbContext, sp) =>
        {
            var service = sp.GetRequiredService<ISettingCenterService>();

            var nameB = await service.GetValueAsync(SettingDefinitions.Keys.CompanyName);
            var alertFpyB = await service.GetValueAsync<int>(SettingDefinitions.Keys.ThresholdAlertFpy);

            Assert.Equal("範例製造股份有限公司", nameB);
            Assert.Equal(95, alertFpyB);
        });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task ExportedJson_ImportedToNewTenant_ResultsInIdenticalSettings()
    {
        string exportedJson = string.Empty;

        // 1. TenantA 設定客製化並匯出
        await ExecuteScopedAsync(TenantAId, async (dbContext, sp) =>
        {
            var service = sp.GetRequiredService<ISettingCenterService>();
            await service.SetValueAsync(SettingDefinitions.Keys.CompanyName, "神準科技股份有限公司");
            await service.SetValueAsync(SettingDefinitions.Keys.CompanyTimeZone, "Asia/Tokyo");
            await service.SetValueAsync(SettingDefinitions.Keys.ThresholdTargetFpy, "99");
            await service.SetValueAsync(SettingDefinitions.Keys.ThresholdLongLeadWeeks, "16");

            exportedJson = await service.ExportSettingsJsonAsync();
            Assert.False(string.IsNullOrWhiteSpace(exportedJson));
        });

        // 2. TenantB（新公司）匯入這份 JSON
        await ExecuteScopedAsync(TenantBId, async (dbContext, sp) =>
        {
            var service = sp.GetRequiredService<ISettingCenterService>();

            await service.ImportSettingsJsonAsync(exportedJson);

            // 驗證 TenantB 的各項設定與 TenantA 完全一致
            var nameB = await service.GetValueAsync(SettingDefinitions.Keys.CompanyName);
            var tzB = await service.GetValueAsync(SettingDefinitions.Keys.CompanyTimeZone);
            var fpyB = await service.GetValueAsync<int>(SettingDefinitions.Keys.ThresholdTargetFpy);
            var leadWeeksB = await service.GetValueAsync<int>(SettingDefinitions.Keys.ThresholdLongLeadWeeks);

            Assert.Equal("神準科技股份有限公司", nameB);
            Assert.Equal("Asia/Tokyo", tzB);
            Assert.Equal(99, fpyB);
            Assert.Equal(16, leadWeeksB);
        });
    }
}


