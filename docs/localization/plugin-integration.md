# Plugin localization integration guide (AssettoServer 0.0.55 / net9.0)

This guide shows how to add `ILocalizationService` support to an AssettoServer plugin so its player-visible strings can be translated. The pattern is used by 5 plugins in this tree — `AutoModerationPlugin`, `RaceChallengePlugin`, `ReportPlugin`, `VotingWeatherPlugin`, `WordFilterPlugin` — read those for living examples.

> **0.0.55 note.** Earlier versions exposed `IAssettoServerAutostart` / `CriticalBackgroundService`; **0.0.55 does not**. A plugin's "main class" here means whatever is constructed **once at startup** — an `IHostedService`, a `BackgroundService`, or an Autofac `SingleInstance()` service. Put `RegisterSource` in that class's constructor.

## TL;DR

1. Add a `<Content Include="lang\*.yml">` block to the plugin's `.csproj`.
2. Constructor-inject `ILocalizationService` into the plugin's startup-singleton class.
3. In that constructor's body, call `RegisterSource(...)` exactly once with a unique namespace.
4. Constructor-inject `ILocalizationService` into any other class that emits player text (controllers, command modules, per-entry-car classes).
5. Replace string literals with `_l10n.Get(key, args)`.
6. Create `lang/<PluginName>.en-US.yml` and `lang/<PluginName>.zh-CN.yml`.

## How it works at runtime

The core registers `YamlLocalizationService` as a singleton in `AssettoServer/Startup.cs` (`builder.RegisterType<YamlLocalizationService>().As<ILocalizationService>().SingleInstance()`). It loads `cfg/lang/*.yml` on construction, so the core `server.*` / `cmd.*` / `lua.*` keys are available immediately.

Each plugin extends the shared dictionary by calling `RegisterSource(dir, namespace)` from its own startup. Because the plugin's startup-singleton class is constructed exactly once when the host starts, that constructor is the natural place to call `RegisterSource`. All keys end up in the same global dictionary keyed by `locale`, so any class with `ILocalizationService` injected can resolve any plugin's keys.

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

After build / publish, the yml files land next to the plugin DLL in `<output>/plugins/<PluginName>/lang/`.

## Step 2 — Inject `ILocalizationService`

```csharp
using AssettoServer.Shared.Services;
```

Add a field and constructor parameter to the plugin's startup-singleton class:

```csharp
private readonly ILocalizationService _l10n;

public MyPlugin(
    MyPluginConfiguration configuration,
    EntryCarManager entryCarManager,
    ILocalizationService l10n)   // <-- new
{
    _l10n = l10n;
    // ...
}
```

### Parameter placement rule

- **If the constructor's tail has no optional parameters**, just append `ILocalizationService l10n` at the end.
- **If the tail has optional parameters** (e.g. `AiSpline? aiSpline = null`), insert `ILocalizationService l10n` *before* them. A required parameter cannot follow an optional one, and giving `l10n` a default (`= null!`) would mask a real DI failure as a silent null — don't.

`AutoModerationPlugin` and `RaceChallengePlugin` both have an optional `AiSpline? aiSpline = null` / `bool lineUpRequired = true` tail; see how `l10n` is inserted before it.

## Step 3 — RegisterSource

Call this **exactly once**, in the startup-singleton class's constructor:

```csharp
var pluginDir = Path.GetDirectoryName(typeof(MyPlugin).Assembly.Location)!;
_l10n.RegisterSource(Path.Combine(pluginDir, "lang"), "myplugin");
```

The namespace argument is informational (used in logs); key uniqueness comes from your YAML keys themselves. Pick a short prefix used by all your keys: `automod`, `race`, `report`, `voteweather`, `wordfilter`.

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

Some plugins use Autofac's automatic factory delegates — e.g. `Func<EntryCar, EntryCarRace>` in `RaceChallengePlugin`, `Func<EntryCar, EntryCarAutoModeration>` in `AutoModerationPlugin`. You **do not** add `ILocalizationService` to the factory delegate signature: Autofac resolves any constructor parameters not listed in the delegate from the container automatically. Just add the parameter to the constructed class's constructor (before any optional trailing parameter) and it works.

See `RaceChallengePlugin/EntryCarRace.cs` + `Race.cs` and `AutoModerationPlugin/EntryCarAutoModeration.cs` for live examples.

## Step 5 — Key naming convention

Hierarchical, dot-separated, all lowercase, prefixed with `plugin.<namespace>`:

```
plugin.<namespace>.<category>.<name>
```

Categories used in existing plugins:

- `reason` — short kick / teleport reasons passed as the `reason` parameter to `EntryCarManager.KickAsync` (core's broadcast/self templates embed these via `{reason}`)
- `warning` — player-facing warnings before action is taken
- `cmd.<command>.<outcome>` — replies from command modules
- `broadcast` / `challenge` / `line_up` etc. — domain-specific groupings

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

**Required:**

- `locale` must be present and match the filename suffix.
- All non-`en-US` files should set `fallback: en-US` so missing keys degrade gracefully.
- Values containing `:`, `*`, single quotes, or leading whitespace must be quoted; quoting everything uniformly is easiest.

### YAML key casing trap

`LocalizationFile` uses `[YamlMember(Alias = "locale")]` (see `AssettoServer/Server/Localization/LocalizationFile.cs`) to map lowercase YAML keys to PascalCase C# properties. **Do not** capitalize the top-level keys — `Locale:` won't bind and the file is silently skipped with a single `Log.Error`. Always use lowercase `locale:` / `fallback:` / `strings:`.

## Localizing client-side Lua (CSP UI)

The in-game CSP UI (the AssettoServer online-extra tab) is driven by `AssettoServer/Server/Lua/assettoserver.lua`. It is localized **server-side, per `ServerLocale`** — the same catalog, one served script for all clients:

- The Lua references strings via `tr("lua.<key>")`, optionally with an args table for placeholders: `tr("lua.toast.value_set", { key = key, value = tostring(value) })`.
- `AssettoServer/Server/Lua/LuaLocalizer.cs` scans the script for `tr("lua.*")` keys, builds a `local __L = { … }` table from the catalog (using `ILocalizationService.GetRaw`, which returns the raw template with `{placeholders}` intact and properly Lua-escaped), prepends a tiny `tr(key, args)` helper, and `ACServer` serves the result.
- Add `lua.*` keys to `cfg/lang/core.{en-US,zh-CN}.yml`.

Use `GetRaw` (not `Get`) whenever you need the **template** rather than a rendered string — i.e. when the placeholder substitution happens somewhere other than C# (as it does in the Lua `tr`).

> **Don't translate** the AGPL license body or strings the client pattern-matches (the Lua countdown `Ready/Set/Go!`, the core `cmd.ballast.syntax_error`). These stay English in every locale.

## Verification

```bash
dotnet build <YourPlugin>/<YourPlugin>.csproj    # 0 errors
dotnet build AssettoServer.slnx                  # full solution still green
dotnet test AssettoServer.Tests/                 # core l10n tests still pass

# Confirm yml files were copied:
ls <YourPlugin>/bin/Debug/net9.0/lang/
```

At server startup, check the Serilog output:

```
[INF] Loaded translations: en-US=N, zh-CN=M
[INF] Registered translation source: myplugin
```

If you see `[missing: plugin.myplugin.foo]` in player chat you've either forgotten/mistyped a key, or hit the `Locale:` casing trap (check logs for "Failed to load translation file").

## Patterns by plugin shape

| Plugin shape | Example | RegisterSource location | Notes |
|---|---|---|---|
| Single startup class, all logic inline | `WordFilterPlugin`, `VotingWeatherPlugin` | that class's ctor | Simplest case |
| Startup class + Qmmands command module | most plugins | startup class ctor | CommandModule gets `ILocalizationService` via Qmmands DI |
| Startup class + HTTP controller + command module | `ReportPlugin` | startup class ctor | Controller gets it via ASP.NET DI |
| Startup class + Autofac factories → per-entry/session state | `RaceChallengePlugin` (`EntryCarRace`, `Race`), `AutoModerationPlugin` (`EntryCarAutoModeration`) | startup class ctor | Factories resolve `ILocalizationService` automatically — no delegate signature change; insert before any optional trailing param |

## Don'ts

- **Don't** call `RegisterSource` more than once per plugin.
- **Don't** put `RegisterSource` in an Autofac `Module.Load` — `ILocalizationService` isn't built yet there.
- **Don't** translate strings other code parses (CSP pattern-matched text). `cmd.ballast.syntax_error` and the Lua countdown deliberately stay English in both locales.
- **Don't** translate AGPL legal notices / `/legal` output — out of scope per repo policy.
- **Don't** put YAML files anywhere except `<PluginDir>/lang/` — the loader's `Assembly.Location` discovery relies on it.
