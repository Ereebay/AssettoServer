# Plugin localization integration guide

This guide shows how to add `ILocalizationService` support to an AssettoServer plugin so its player-visible strings can be translated. The pattern was validated across 5 plugins (`AutoModerationPlugin`, `RaceChallengePlugin`, `ReportPlugin`, `VotingWeatherPlugin`, `WordFilterPlugin`) — read those for living examples.

## TL;DR

1. Add a `<Content Include="lang\*.yml">` block to the plugin's `.csproj`
2. Constructor-inject `ILocalizationService` into the plugin's `IAssettoServerAutostart` main class
3. In that constructor's body, call `RegisterSource(...)` exactly once with a unique namespace
4. Constructor-inject `ILocalizationService` into any other class that needs to emit player text (controllers, command modules, per-entry-car classes)
5. Replace string literals with `_l10n.Get(key, args)`
6. Create `lang/<PluginName>.en-US.yml` and `lang/<PluginName>.zh-CN.yml`

## How it works at runtime

The core registers `YamlLocalizationService` as a singleton in `AssettoServer/Network/Http/Startup.cs`. It loads `cfg/lang/*.yml` on construction (so the core `server.*` / `cmd.*` keys are available immediately).

Each plugin extends the shared dictionary by calling `RegisterSource(dir, namespace)` from its own startup. The plugin's main class — the one implementing `IAssettoServerAutostart` — is constructed exactly once when the host starts, which makes it the natural place to call `RegisterSource`. All keys end up in the same global dictionary keyed by `locale`, so any class with `ILocalizationService` injected can resolve any plugin's keys.

`ServerLocale` from `extra_cfg.yml` controls which locale is read first. Missing keys fall back through the `fallback:` chain in each YAML file, ultimately to `en-US`.

## Step 1 — csproj

Append this `ItemGroup` to your plugin's `.csproj`:

```xml
<ItemGroup>
    <Content Include="lang\*.yml">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </Content>
</ItemGroup>
```

After build / publish, yml files land next to the plugin DLL in `<output>/plugins/<PluginName>/lang/`.

## Step 2 — Inject `ILocalizationService`

```csharp
using AssettoServer.Shared.Services;
```

Add a field and constructor parameter to the plugin's main class:

```csharp
private readonly ILocalizationService _l10n;

public MyPlugin(
    MyPluginConfiguration configuration,
    EntryCarManager entryCarManager,
    IHostApplicationLifetime applicationLifetime,
    ILocalizationService l10n)  // <-- new
    : base(applicationLifetime)
{
    _l10n = l10n;
    // ...
}
```

### Parameter placement rule

This caught us once during validation — follow it to keep things clean:

- **If the existing constructor's tail has no optional parameters** (no `= default` / `= null`), just append `ILocalizationService l10n` at the end.
- **If the tail has optional parameters** (e.g. `AiSpline? aiSpline = null`), insert `ILocalizationService l10n` *before* them. Do NOT append after and give it a default value — that works at runtime but masks a real DI failure as a silent null.

`AutoModerationPlugin.cs` has the old `= null!` pattern preserved as a known mild wart; do not copy that style for new code.

## Step 3 — RegisterSource

Call this **exactly once**, at the end of the plugin main class's constructor:

```csharp
var pluginDir = Path.GetDirectoryName(typeof(MyPlugin).Assembly.Location)!;
_l10n.RegisterSource(Path.Combine(pluginDir, "lang"), "myplugin");
```

The namespace argument is informational (used in logs) — actual key uniqueness is enforced by your YAML keys themselves. Pick a short namespace that prefixes all your keys: `automod`, `race`, `report`, `voteweather`, `wordfilter`.

## Step 4 — Inject into other classes

Plugins emit player-visible text from many places — HTTP controllers, Qmmands command modules, per-entry-car state objects, etc. All of them get `ILocalizationService` via standard DI:

```csharp
public class MyController : ControllerBase
{
    private readonly ILocalizationService _l10n;
    public MyController(ILocalizationService l10n) { _l10n = l10n; }
}
```

### Plugins with Autofac delegate factories

Some plugins use Autofac's automatic factory delegates — e.g. `Func<EntryCar, EntryCarRace>` in `RaceChallengePlugin`. You **do not** need to add `ILocalizationService` to the factory delegate signature: Autofac resolves any constructor parameters not listed in the delegate from the container automatically. Just add the parameter to the constructed class's constructor and it works.

See `RaceChallengePlugin/EntryCarRace.cs` and `Race.cs` for live examples.

## Step 5 — Key naming convention

Hierarchical, dot-separated, all lowercase, prefixed with `plugin.<namespace>`:

```
plugin.<namespace>.<category>.<name>
```

Categories used in existing plugins:

- `reason` — short kick / ban reasons passed as the `reason` parameter to `EntryCarManager.KickAsync` (core's broadcast template embeds these)
- `warning` — player-facing warnings before action is taken
- `cmd.<command>.<outcome>` — replies from command modules
- `broadcast` — server-wide announcements
- `msg` / `error` — generic messages / errors

Use `{name}`-style named placeholders, not `{0}`. The renderer reads matching properties from an anonymous object by reflection:

```csharp
_l10n.Get("plugin.race.challenge.sent", new { name = target.Name });
```

## Step 6 — YAML files

`lang/<PluginName>.en-US.yml`:

```yaml
locale: en-US
strings:
  plugin.myplugin.cmd.hello: "Hello, {name}!"
  plugin.myplugin.warning.shutdown: "Server going down in {seconds} seconds."
```

`lang/<PluginName>.zh-CN.yml`:

```yaml
locale: zh-CN
fallback: en-US
strings:
  plugin.myplugin.cmd.hello: "你好，{name}！"
  plugin.myplugin.warning.shutdown: "服务器将在 {seconds} 秒后关闭。"
```

**Required**:

- `locale` field must be present and match the filename suffix
- All non-`en-US` files should have `fallback: en-US` so missing keys degrade gracefully
- File encoding: UTF-8 without BOM
- Line endings: CRLF (enforced by repo `.editorconfig`)
- Values containing `:`, `*`, single quotes, or leading whitespace must be double-quoted; using double quotes uniformly is easiest

### YAML key casing trap

The POCO `LocalizationFile` uses `[YamlMember(Alias = "locale")]` to map lowercase YAML keys to PascalCase C# properties. This is wired up in `AssettoServer/Server/Localization/LocalizationFile.cs`. **Do not** capitalize the top-level keys in your translation files — `Locale:` (PascalCase) won't bind, and your file will be silently skipped with a single `Log.Error`. Always use `locale:` / `fallback:` / `strings:` lowercase.

## Verification

```bash
dotnet build <YourPlugin>/<YourPlugin>.csproj   # 0 error
dotnet build AssettoServer.sln                  # full solution still green
dotnet test AssettoServer.Tests/                # core l10n tests still pass

# Confirm yml files were copied:
ls <YourPlugin>/bin/Debug/net8.0/lang/
```

At server startup, check the Serilog output:

```
[INF] Loaded translations: en-US=N, zh-CN=M
[INF] Registered translation source: myplugin
```

If you see `[missing: plugin.myplugin.foo]` in player chat, you've either:
- forgotten a key in your YAML file
- mistyped the key in code or YAML
- the `Locale:` casing trap (see above) — check server logs for "Failed to load translation file"

## Patterns by plugin shape

| Plugin shape | Example | RegisterSource location | Notes |
|---|---|---|---|
| Single main class, all logic inline | `WordFilterPlugin`, `VotingWeatherPlugin`, `AutoModerationPlugin` | Main class ctor end | Simplest case |
| Main class + Qmmands command module | (any plugin with a `*CommandModule.cs`) | Main class ctor end | CommandModule gets `ILocalizationService` via Qmmands DI |
| Main class + HTTP controller + CommandModule | `ReportPlugin` | Main class ctor end | Controller gets it via ASP.NET DI |
| Main class + Autofac factories spawning per-entry / per-session state | `RaceChallengePlugin` (`EntryCarRace`, `Race`) | Main class ctor end | Factories resolve `ILocalizationService` automatically — no delegate signature change needed |

## Don'ts

- **Don't** call `RegisterSource` more than once per plugin — it's a no-op the second time but adds confusion
- **Don't** put `RegisterSource` in an Autofac `Module.Load` — `ILocalizationService` isn't built yet at that point
- **Don't** translate strings that other code parses (e.g. anything CSP clients pattern-match on). The `cmd.ballast.syntax_error` key in core deliberately holds English in both locales for this reason
- **Don't** translate AGPL legal notices (`LegalNotice.cs`, `/legal` output) — out of scope per repo policy
- **Don't** put your YAML files anywhere except `<PluginDir>/lang/`. The plugin loader's `Assembly.Location` discovery relies on this path
