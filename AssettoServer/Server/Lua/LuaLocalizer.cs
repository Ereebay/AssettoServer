using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using AssettoServer.Shared.Services;

namespace AssettoServer.Server.Lua;

/// <summary>
/// Localizes a CSP server Lua script for the active <c>ServerLocale</c>. The script references strings via
/// <c>tr("lua.some.key")</c> (optionally with an args table for {placeholders}); this prepends a translation
/// table built from the localization catalog plus a small <c>tr()</c> helper, so the served script carries the
/// right language. Server-wide (one served script for all clients), consistent with the C# localization.
/// </summary>
public static class LuaLocalizer
{
    // Matches tr("some.key") calls. Keys are dotted lowercase (core "lua.*" or plugin "plugin.<name>.*").
    private static readonly Regex TrKeyRegex = new(@"tr\(""([\w.]+)""", RegexOptions.Compiled);

    public static string Inject(string luaSource, ILocalizationService localization)
    {
        var keys = new HashSet<string>();
        foreach (Match match in TrKeyRegex.Matches(luaSource))
        {
            keys.Add(match.Groups[1].Value);
        }

        var sb = new StringBuilder();
        sb.Append("local __L = {\n");
        foreach (var key in keys)
        {
            sb.Append("  [\"").Append(key).Append("\"] = \"").Append(Escape(localization.GetRaw(key))).Append("\",\n");
        }
        sb.Append("}\n");
        sb.Append("local function tr(key, args)\n");
        sb.Append("  local s = __L[key] or key\n");
        sb.Append("  if args ~= nil then\n");
        sb.Append("    s = s:gsub(\"{(%w+)}\", function(k) local v = args[k]; if v ~= nil then return tostring(v) else return \"{\" .. k .. \"}\" end end)\n");
        sb.Append("  end\n");
        sb.Append("  return s\n");
        sb.Append("end\n");
        sb.Append(luaSource);
        return sb.ToString();
    }

    private static string Escape(string s) => s
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");
}
