using Core.Settings;
using Core.UserPreference;
using IntegrationTests.Isolation;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace IntegrationTests.UserPreference;

public class UserPreferenceTests : IsolationTestBase
{
    [Fact]
    [Trait("Category", "")]
    public void SettingDefinitions_ContainsViewModeSettings_WithCorrectDefaults()
    {
        var defaultMode = SettingDefinitions.Find(SettingDefinitions.Keys.ViewModeDefault);
        Assert.NotNull(defaultMode);
        Assert.Equal("relaxed", defaultMode.DefaultValue);
        Assert.Equal(SettingCategories.ViewMode, defaultMode.Category);

        var allowSwitch = SettingDefinitions.Find(SettingDefinitions.Keys.ViewModeAllowSwitch);
        Assert.NotNull(allowSwitch);
        Assert.Equal("true", allowSwitch.DefaultValue);

        var allowMotion = SettingDefinitions.Find(SettingDefinitions.Keys.ViewModeAllowMotion);
        Assert.NotNull(allowMotion);
        Assert.Equal("true", allowMotion.DefaultValue);

        var motionDefault = SettingDefinitions.Find(SettingDefinitions.Keys.ViewModeMotionDefault);
        Assert.NotNull(motionDefault);
        Assert.Equal("true", motionDefault.DefaultValue);

        var followReduced = SettingDefinitions.Find(SettingDefinitions.Keys.ViewModeFollowReducedMotion);
        Assert.NotNull(followReduced);
        Assert.Equal("true", followReduced.DefaultValue);

        var numberCountUp = SettingDefinitions.Find(SettingDefinitions.Keys.ViewModeNumberCountUp);
        Assert.NotNull(numberCountUp);
        Assert.Equal("false", numberCountUp.DefaultValue);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task UserPreferenceApi_SetAndGet_PersistsUserChoices()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("__tenant", TenantAId.ToString());

        var updatePayload = new UpdateUserUiPreferenceInput(
            ViewMode: "overview",
            Motion: "off",
            LastProjectCode: "NPI-2601");

        var putRes = await client.PutAsJsonAsync("/api/user-preference?userId=user_001", updatePayload);
        putRes.EnsureSuccessStatusCode();

        var getRes = await client.GetAsync("/api/user-preference?userId=user_001");
        getRes.EnsureSuccessStatusCode();

        var pref = await getRes.Content.ReadFromJsonAsync<UserUiPreferenceDto>();
        Assert.NotNull(pref);
        Assert.Equal("overview", pref.ViewMode);
        Assert.Equal("off", pref.Motion);
        Assert.Equal("NPI-2601", pref.LastProjectCode);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task UserPreference_MultiTenantIsolation_TenantBCannotSeeTenantA()
    {
        var clientA = Factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("__tenant", TenantAId.ToString());

        var updatePayloadA = new UpdateUserUiPreferenceInput(
            ViewMode: "overview",
            Motion: "off",
            LastProjectCode: "PROJ-A");

        var putResA = await clientA.PutAsJsonAsync("/api/user-preference?userId=shared_user", updatePayloadA);
        putResA.EnsureSuccessStatusCode();

        // Tenant B with same user ID
        var clientB = Factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("__tenant", TenantBId.ToString());

        var getResB = await clientB.GetAsync("/api/user-preference?userId=shared_user");
        getResB.EnsureSuccessStatusCode();

        var prefB = await getResB.Content.ReadFromJsonAsync<UserUiPreferenceDto>();
        Assert.NotNull(prefB);
        // Tenant B has not set any preference, should be default null
        Assert.Null(prefB.ViewMode);
        Assert.Null(prefB.Motion);
        Assert.Null(prefB.LastProjectCode);
    }
}


