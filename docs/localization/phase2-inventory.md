# Phase 2 — Core Server String Migration Inventory

Authoritative checklist for migrating all player-visible strings in the core server (excluding plugins, Lua, LegalNotice, AGPL text) to `ILocalizationService.Get(key, args)`.

54 strings total. Naming convention: namespace prefix (`server` / `kick` / `ban` / `afk` / `cmd.<command>` / `ai` / `auth`), `{name}`-style named placeholders.

## Edge case rulings (finalized)

1. **`/forcelights` ternary** — split into two keys instead of passing `{state}`:
   - `cmd.forcelights.enabled` — "`{name}`'s lights will be forced on."
   - `cmd.forcelights.disabled` — "`{name}`'s lights will not be forced on."
2. **`/set` exception message** — wrap with `cmd.set.error` taking `{error}` param. Exception text itself is **not** translated (it's runtime system text).
3. **`/say` prefix** — only translate `"CONSOLE: "` prefix via `{message}` param. Admin input passes through raw.
4. **`/distance`** — keep `CultureInfo.InvariantCulture` for the number. Pass formatted number as `{distance}` string param.
5. **`/legal`** — out of scope. Leaves `LegalNotice.LegalNoticeText` untouched.

## Migration table

For any row where "Current string" looks incomplete, the implementer must `Read` the file at the cited line to extract the full surrounding `$"..."` expression before substituting.

| File:Line | Current string | Suggested key | Params |
|---|---|---|---|
| **1. Lifecycle & system broadcast** | | | |
| `Server/ACServer.cs:122` | `*** Server shutting down ***` | `server.shutdown_broadcast` | – |
| **2. Kick / ban — broadcast** | | | |
| `Server/EntryCarManager.cs:64` | `{name} has been kicked from the server for {reason}.` | `kick.broadcast_with_reason` | `name`, `reason` |
| `Server/EntryCarManager.cs:64` | `{name} has been kicked from the server.` | `kick.broadcast_no_reason` | `name` |
| `Server/EntryCarManager.cs:74` | `{name} has been banned from the server for {reason}.` | `ban.broadcast_with_reason` | `name`, `reason` |
| `Server/EntryCarManager.cs:74` | `{name} has been banned from the server.` | `ban.broadcast_no_reason` | `name` |
| `Network/Tcp/ACTcpClient.cs:884` | `{name} failed the checksum check and has been kicked.` | `kick.broadcast_checksum_failed` | `name` |
| **3. Kick / ban — self** | | | |
| `Server/EntryCarManager.cs:63` | `You have been kicked for {reason}` | `kick.self_with_reason` | `reason` |
| `Server/EntryCarManager.cs:73` | `You have been banned for {reason}` | `ban.self_with_reason` | `reason` |
| `Server/EntryCarManager.cs:73` | `You have been banned from the server` | `ban.self_no_reason` | – |
| `Network/Udp/UdpPluginServer.cs:300` | `You have been kicked.` | `kick.plugin_default` | – |
| **4. AFK** | | | |
| `Server/EntryCar.cs:142` | `You will be kicked in 1 minute for being AFK.` | `afk.warning_1min` | – |
| **5. AdminModule** | | | |
| `Commands/Modules/AdminModule.cs:47` | `You cannot kick yourself.` | `cmd.kick.cannot_self` | – |
| `Commands/Modules/AdminModule.cs:49` | `You cannot kick an administrator` | `cmd.kick.cannot_admin` | – |
| `Commands/Modules/AdminModule.cs:52` | `Steam profile of {name}: https://steamcommunity.com/profiles/{guid}` | `cmd.kick.steam_profile` | `name`, `guid` |
| `Commands/Modules/AdminModule.cs:63` | `You cannot ban yourself.` | `cmd.ban.cannot_self` | – |
| `Commands/Modules/AdminModule.cs:65` | `You cannot ban an administrator.` | `cmd.ban.cannot_admin` | – |
| `Commands/Modules/AdminModule.cs:68` | `Steam profile of {name}: https://steamcommunity.com/profiles/{guid}` | `cmd.ban.steam_profile` | `name`, `guid` |
| `Commands/Modules/AdminModule.cs:71` | `{name} is using Steam Family Sharing, banning game owner https://steamcommunity.com/profiles/{guid}` | `cmd.ban.family_sharing_notice` | `name`, `guid` |
| `Commands/Modules/AdminModule.cs:83` | `You have been teleported to the pits.` | `cmd.pit.self` | – |
| `Commands/Modules/AdminModule.cs:86` | `{name} has been teleported to the pits.` | `cmd.pit.broadcast` | `name` |
| `Commands/Modules/AdminModule.cs:95` | `Time has been set.` | `cmd.settime.success` | – |
| `Commands/Modules/AdminModule.cs:99` | `Invalid time format. Usage: /settime 15:31` | `cmd.settime.invalid_format` | – |
| `Commands/Modules/AdminModule.cs:108` | `Weather configuration has been set.` | `cmd.setweather.success` | – |
| `Commands/Modules/AdminModule.cs:112` | `There is no weather configuration with this id.` | `cmd.setweather.not_found` | – |
| `Commands/Modules/AdminModule.cs:119` | `Available weathers:` | `cmd.cspweather.list_header` | – |
| `Commands/Modules/AdminModule.cs:122` | ` - {type}` | `cmd.cspweather.list_item` | `type` |
| `Commands/Modules/AdminModule.cs:132` | `Weather has been set.` | `cmd.setcspweather.success` | – |
| `Commands/Modules/AdminModule.cs:136` | `No weather with name '{name}', use /cspweather for a list of available weathers.` | `cmd.setcspweather.not_found` | `name` |
| `Commands/Modules/AdminModule.cs:159` | `Distance: {distance}` (verify exact format in file) | `cmd.distance.result` | `distance` |
| `Commands/Modules/AdminModule.cs:168` | `{name}'s lights will be forced on.` | `cmd.forcelights.enabled` | `name` |
| `Commands/Modules/AdminModule.cs:168` | `{name}'s lights will not be forced on.` | `cmd.forcelights.disabled` | `name` |
| `Commands/Modules/AdminModule.cs:174` | `IP: {ip}\nProfile: https://steamcommunity.com/profiles/{guid}\nPing: {ping}ms` | `cmd.whois.info` | `ip`, `guid`, `ping` |
| `Commands/Modules/AdminModule.cs:175` | `Position: {position}\nVelocity: {velocity}kmh` | `cmd.whois.position` | `position`, `velocity` |
| `Commands/Modules/AdminModule.cs:178` | `Steam Family Sharing Owner: https://steamcommunity.com/profiles/{guid}` | `cmd.whois.family_sharing_owner` | `guid` |
| `Commands/Modules/AdminModule.cs:186` | `Restrictor and ballast set.` | `cmd.restrict.success` | – |
| `Commands/Modules/AdminModule.cs:193` | `SYNTAX ERROR: Use 'ballast [driver numeric id] [kg]'` | `cmd.ballast.syntax_error` | – |
| `Commands/Modules/AdminModule.cs:201` | `Property {key} set to {value}` | `cmd.set.success` | `key`, `value` |
| `Commands/Modules/AdminModule.cs:201` | `Could not set property {key}` | `cmd.set.failed` | `key` |
| `Commands/Modules/AdminModule.cs:205` | (wraps `ex.Message`) | `cmd.set.error` | `error` |
| `Commands/Modules/AdminModule.cs:213` | `SteamID {guid} was added to the whitelist` | `cmd.whitelist.added` | `guid` |
| `Commands/Modules/AdminModule.cs:219` | `CONSOLE: {message}` | `cmd.say.broadcast` | `message` |
| **6. GeneralModule** | | | |
| `Commands/Modules/GeneralModule.cs:27` | `Pong! {ping}ms.` | `cmd.ping.result` | `ping` |
| `Commands/Modules/GeneralModule.cs:31` | `It is currently {time}.` | `cmd.time.result` | `time` |
| `Commands/Modules/GeneralModule.cs:48` | `You are now Admin for this server` | `cmd.admin.success` | – |
| `Commands/Modules/GeneralModule.cs:51` | `Command refused` | `cmd.admin.refused` | – |
| **7. AiTrafficModule** | | | |
| `Commands/Modules/AiTrafficModule.cs:28` | `AI disabled` | `cmd.setaioverbooking.ai_disabled` | – |
| `Commands/Modules/AiTrafficModule.cs:36` | `AI overbooking set to {count}` | `cmd.setaioverbooking.success` | `count` |
| **8. Command framework** | | | |
| `Commands/ChatService.cs:93` | `An error occurred while executing this command.` | `cmd.execution_error` | – |
| `Commands/Attributes/RequireAdminAttribute.cs:37` | `You are not an administrator.` | `cmd.permission_denied_admin` | – |
| **9. Connection / auth filters** | | | |
| `Server/OpenSlotFilters/SteamSlotFilter.cs:21` | `Steam authentication failed.` | `auth.steam_failed` | – |
| `Server/OpenSlotFilters/WhitelistSlotFilter.cs:22` | `You are not whitelisted on this server` | `auth.not_whitelisted` | – |
| `Network/Tcp/ACTcpClient.cs:350` | `Driver name cannot be empty.` | `auth.empty_name` | – |
| `Network/Tcp/ACTcpClient.cs:352` | `Missing CSP features. Please update CSP and/or Content Manager.` | `auth.missing_csp_features` | – |

## Out of scope (do not touch)

- `Server/LegalNotice.cs` (entire file)
- `/legal` command body in `GeneralModule.cs`
- `Server/Lua/assettoserver.lua` (Phase 4)
- All `Log.*` calls
- All `[YamlMember(Description = ...)]` config descriptions
- Plugin directories (separate per-plugin work in Phase 5)
