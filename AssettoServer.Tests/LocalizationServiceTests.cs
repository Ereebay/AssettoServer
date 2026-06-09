using System.IO;
using AssettoServer.Server.Localization;
using AssettoServer.Server.Lua;
using AssettoServer.Shared.Weather;

namespace AssettoServer.Tests;

public class LocalizationServiceTests
{
    private static string FindLangDir()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "AssettoServer", "cfg", "lang");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("Could not locate AssettoServer/cfg/lang from test runner cwd.");
    }

    [Test]
    public void LoadsEnUs_AllKeysPresent()
    {
        var svc = new YamlLocalizationService("en-US", FindLangDir());

        Assert.That(svc.Get("server.shutdown_broadcast"), Is.EqualTo("*** Server shutting down ***"));
        Assert.That(svc.Get("afk.warning_1min"), Is.EqualTo("You will be kicked in 1 minute for being AFK."));
        Assert.That(svc.Get("auth.empty_name"), Is.EqualTo("Driver name cannot be empty."));
    }

    [Test]
    public void LoadsZhCn_AllKeysPresent()
    {
        var svc = new YamlLocalizationService("zh-CN", FindLangDir());

        Assert.That(svc.Get("server.shutdown_broadcast"), Is.EqualTo("*** 服务器即将关闭 ***"));
        Assert.That(svc.Get("afk.warning_1min"), Is.EqualTo("你将在 1 分钟后因挂机被踢出。"));
        Assert.That(svc.Get("auth.empty_name"), Is.EqualTo("驾驶员名称不能为空。"));
    }

    [Test]
    public void PlaceholderRendering_NamedAnonymousObject()
    {
        var svc = new YamlLocalizationService("en-US", FindLangDir());

        Assert.That(svc.Get("cmd.ping.result", new { ping = 42 }), Is.EqualTo("Pong! 42ms."));
        Assert.That(svc.Get("kick.broadcast_with_reason", new { name = "Bob", reason = "cheating" }),
            Is.EqualTo("Bob has been kicked from the server for cheating."));
    }

    [Test]
    public void PlaceholderRendering_ZhCn_PreservesOrderAndUnits()
    {
        var svc = new YamlLocalizationService("zh-CN", FindLangDir());

        Assert.That(svc.Get("cmd.ping.result", new { ping = 42 }), Is.EqualTo("Pong！42ms。"));
        Assert.That(svc.Get("kick.broadcast_with_reason", new { name = "张三", reason = "作弊" }),
            Is.EqualTo("张三 被踢出服务器，原因：作弊。"));
    }

    [Test]
    public void Fallback_ZhCnMissingKey_FallsBackToEnUs()
    {
        var svc = new YamlLocalizationService("zh-CN", FindLangDir());

        // ballast SYNTAX ERROR must stay English in BOTH locales (CSP admin detection)
        // This also exercises the path where zh-CN HAS the key, just with English content.
        Assert.That(svc.Get("cmd.ballast.syntax_error"),
            Is.EqualTo("SYNTAX ERROR: Use 'ballast [driver numeric id] [kg]'"));
    }

    [Test]
    public void MissingKey_ReturnsMissingMarker()
    {
        var svc = new YamlLocalizationService("en-US", FindLangDir());

        Assert.That(svc.Get("this.key.does.not.exist"), Is.EqualTo("[missing: this.key.does.not.exist]"));
    }

    [Test]
    public void MissingDirectory_DoesNotThrow()
    {
        var svc = new YamlLocalizationService("en-US", Path.Combine(Path.GetTempPath(), "definitely-not-here-" + System.Guid.NewGuid()));

        Assert.That(svc.Get("any.key"), Is.EqualTo("[missing: any.key]"));
    }

    [Test]
    public void EmptyLocale_DefaultsToEnUs()
    {
        var svc = new YamlLocalizationService("", FindLangDir());

        Assert.That(svc.Get("server.shutdown_broadcast"), Is.EqualTo("*** Server shutting down ***"));
    }

    [Test]
    public void WhoisInfo_NewlinesPreserved()
    {
        var svc = new YamlLocalizationService("zh-CN", FindLangDir());
        var result = svc.Get("cmd.whois.info", new { ip = "1.2.3.4", guid = 76561198000000000UL, ping = 50 });

        Assert.That(result, Contains.Substring("\n"));
        Assert.That(result, Does.StartWith("IP："));
        Assert.That(result, Does.EndWith("Ping：50ms"));
    }

    [Test]
    public void GetRaw_PreservesPlaceholders()
    {
        var svc = new YamlLocalizationService("en-US", FindLangDir());
        Assert.That(svc.GetRaw("lua.toast.value_set"), Is.EqualTo("{key} set to {value}"));
    }

    [Test]
    public void LuaLocalizer_InjectsTableAndHelper()
    {
        var svc = new YamlLocalizationService("zh-CN", FindLangDir());
        var result = LuaLocalizer.Inject("ui.tabItem(tr(\"lua.tab.about\"), x)", svc);

        Assert.That(result, Contains.Substring("local function tr"));
        Assert.That(result, Contains.Substring("[\"lua.tab.about\"] = \"关于\""));
        Assert.That(result, Does.EndWith("ui.tabItem(tr(\"lua.tab.about\"), x)"));
    }

    [Test]
    public void WeatherType_LocalizedName()
    {
        var en = new YamlLocalizationService("en-US", FindLangDir());
        var zh = new YamlLocalizationService("zh-CN", FindLangDir());

        Assert.That(WeatherFxType.LightRain.Localized(en), Is.EqualTo("Light Rain"));
        Assert.That(WeatherFxType.LightRain.Localized(zh), Is.EqualTo("小雨"));
        Assert.That(WeatherFxType.Clear.Localized(zh), Is.EqualTo("晴"));
    }
}
