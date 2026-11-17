using System.Reflection;
using System.Text.Json;
using Xunit;

namespace IntegrationTests.Localization;

public class LocalizationTests
{
    private static readonly string[] CoreTerms =
    {
        "Project",
        "Stage",
        "Issue",
        "Blocker",
        "Part",
        "Station",
        "Yield"
    };

    private static Dictionary<string, string> LoadBackendTexts(string fileName)
    {
        var assembly = Assembly.Load("Core");
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            throw new FileNotFoundException($"Cannot find embedded resource: {fileName}");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var doc = JsonDocument.Parse(stream);
        var texts = doc.RootElement.GetProperty("texts");

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in texts.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.GetString() ?? string.Empty;
        }

        return dict;
    }

    private static Dictionary<string, string> LoadFrontendTexts(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? frontendPath = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Web", "src", "locales", fileName);
            if (File.Exists(candidate))
            {
                frontendPath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        if (frontendPath == null)
        {
            var relative = Path.Combine("src", "Web", "src", "locales", fileName);
            if (File.Exists(relative)) frontendPath = Path.GetFullPath(relative);
        }

        if (frontendPath == null || !File.Exists(frontendPath))
        {
            throw new FileNotFoundException($"Cannot find frontend locale file: {fileName}");
        }

        var json = File.ReadAllText(frontendPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
               ?? new Dictionary<string, string>();
    }

    [Fact]
    [Trait("Category", "")]
    public void Backend_BothCulturesContainAllCoreTerms()
    {
        var zh = LoadBackendTexts("zh-TW.json");
        var en = LoadBackendTexts("en-US.json");

        foreach (var term in CoreTerms)
        {
            Assert.True(zh.ContainsKey(term), $"Backend zh-TW is missing core term: {term}");
            Assert.True(en.ContainsKey(term), $"Backend en-US is missing core term: {term}");
            Assert.False(string.IsNullOrWhiteSpace(zh[term]));
            Assert.False(string.IsNullOrWhiteSpace(en[term]));
        }

        Assert.Equal("專案", zh["Project"]);
        Assert.Equal("關卡", zh["Stage"]);
        Assert.Equal("問題", zh["Issue"]);
        Assert.Equal("卡關", zh["Blocker"]);
        Assert.Equal("料件", zh["Part"]);
        Assert.Equal("工站", zh["Station"]);
        Assert.Equal("良率", zh["Yield"]);

        Assert.Equal("Project", en["Project"]);
        Assert.Equal("Stage", en["Stage"]);
        Assert.Equal("Issue", en["Issue"]);
        Assert.Equal("Blocker", en["Blocker"]);
        Assert.Equal("Part", en["Part"]);
        Assert.Equal("Station", en["Station"]);
        Assert.Equal("Yield", en["Yield"]);
    }

    [Fact]
    [Trait("Category", "")]
    public void Backend_ZhAndEnKeys_MatchExactlyWithoutMissingKeys()
    {
        // 驗收條件：後端繁中和英文 key 數量完全相同，沒有缺漏
        var zh = LoadBackendTexts("zh-TW.json");
        var en = LoadBackendTexts("en-US.json");

        var zhKeys = zh.Keys.OrderBy(k => k).ToList();
        var enKeys = en.Keys.OrderBy(k => k).ToList();

        Assert.NotEmpty(zhKeys);
        Assert.Equal(zhKeys.Count, enKeys.Count);
        Assert.Equal(zhKeys, enKeys);
    }

    [Fact]
    [Trait("Category", "")]
    public void Frontend_BothCulturesContainAllCoreTerms()
    {
        var zh = LoadFrontendTexts("zh-TW.json");
        var en = LoadFrontendTexts("en-US.json");

        foreach (var term in CoreTerms)
        {
            Assert.True(zh.ContainsKey(term), $"Frontend zh-TW is missing core term: {term}");
            Assert.True(en.ContainsKey(term), $"Frontend en-US is missing core term: {term}");
            Assert.False(string.IsNullOrWhiteSpace(zh[term]));
            Assert.False(string.IsNullOrWhiteSpace(en[term]));
        }
    }

    [Fact]
    [Trait("Category", "")]
    public void Frontend_ZhAndEnKeys_MatchExactlyWithoutMissingKeys()
    {
        // 驗收條件：前端繁中和英文 key 數量完全相同，沒有缺漏
        var zh = LoadFrontendTexts("zh-TW.json");
        var en = LoadFrontendTexts("en-US.json");

        var zhKeys = zh.Keys.OrderBy(k => k).ToList();
        var enKeys = en.Keys.OrderBy(k => k).ToList();

        Assert.NotEmpty(zhKeys);
        Assert.Equal(zhKeys.Count, enKeys.Count);
        Assert.Equal(zhKeys, enKeys);
    }

    [Fact]
    [Trait("Category", "")]
    public void BackendAndFrontend_ShareSameCoreKeys()
    {
        // 跨端一致性：前後端均包含相同的核心名詞 keys
        var backendZh = LoadBackendTexts("zh-TW.json");
        var frontendZh = LoadFrontendTexts("zh-TW.json");

        foreach (var term in CoreTerms)
        {
            Assert.True(backendZh.ContainsKey(term));
            Assert.True(frontendZh.ContainsKey(term));
            Assert.Equal(backendZh[term], frontendZh[term]);
        }
    }
}


