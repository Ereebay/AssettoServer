using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Services;
using Serilog;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Localization;

public class YamlLocalizationService : ILocalizationService
{
    private const string DefaultLocale = "en-US";

    private readonly string _currentLocale;
    private readonly Dictionary<string, Dictionary<string, CompiledFormat>> _strings = new();
    private readonly Dictionary<string, string?> _fallbacks = new();
    private readonly ConcurrentDictionary<string, byte> _warnedMissing = new();
    private readonly ConcurrentDictionary<string, byte> _warnedMissingParam = new();
    private readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _argPropertyCache = new();

    private static readonly Regex PlaceholderRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public YamlLocalizationService(ACServerConfiguration configuration)
    {
        _currentLocale = string.IsNullOrEmpty(configuration.Extra.ServerLocale) ? DefaultLocale : configuration.Extra.ServerLocale;

        var langDir = Path.Join(configuration.BaseFolder, "lang");
        if (!Directory.Exists(langDir))
        {
            Log.Information("Localization directory {Path} not found, no translations loaded", langDir);
        }
        else
        {
            LoadDirectory(langDir);
        }

        var summary = new StringBuilder();
        foreach (var (locale, dict) in _strings)
        {
            if (summary.Length > 0) summary.Append(", ");
            summary.Append(locale).Append('=').Append(dict.Count);
        }
        Log.Information("Loaded translations: {Translations}", summary.Length == 0 ? "(none)" : summary.ToString());
    }

    public string Get(string key, object? args = null)
    {
        var format = Resolve(key);
        if (format == null)
        {
            if (_warnedMissing.TryAdd(key, 0))
                Log.Warning("Missing translation key: {Key}", key);
            return $"[missing: {key}]";
        }

        return Render(format, key, args);
    }

    public void RegisterSource(string sourceDir, string @namespace)
    {
        if (!Directory.Exists(sourceDir))
            return;

        Log.Information("Registered translation source: {Namespace}", @namespace);
        LoadDirectory(sourceDir);
    }

    private void LoadDirectory(string dir)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var file in Directory.EnumerateFiles(dir, "*.yml"))
        {
            LocalizationFile? parsed;
            try
            {
                using var stream = File.OpenText(file);
                parsed = deserializer.Deserialize<LocalizationFile>(stream);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load translation file {Path}", file);
                continue;
            }

            if (parsed == null || string.IsNullOrEmpty(parsed.Locale))
            {
                Log.Error("Translation file {Path} has no locale set", file);
                continue;
            }

            if (!_strings.TryGetValue(parsed.Locale, out var bucket))
            {
                bucket = new Dictionary<string, CompiledFormat>();
                _strings[parsed.Locale] = bucket;
            }

            if (!_fallbacks.ContainsKey(parsed.Locale) || _fallbacks[parsed.Locale] == null)
                _fallbacks[parsed.Locale] = parsed.Fallback;

            foreach (var (k, v) in parsed.Strings)
            {
                if (bucket.ContainsKey(k))
                    Log.Warning("Translation key {Key} in locale {Locale} overridden by {Path}", k, parsed.Locale, file);
                bucket[k] = Compile(v);
            }
        }
    }

    private CompiledFormat? Resolve(string key)
    {
        var visited = new HashSet<string>();
        var locale = _currentLocale;
        while (locale != null && visited.Add(locale))
        {
            if (_strings.TryGetValue(locale, out var bucket) && bucket.TryGetValue(key, out var f))
                return f;
            _fallbacks.TryGetValue(locale, out var next);
            locale = next;
        }

        if (!visited.Contains(DefaultLocale) && _strings.TryGetValue(DefaultLocale, out var def) && def.TryGetValue(key, out var df))
            return df;

        return null;
    }

    private string Render(CompiledFormat format, string key, object? args)
    {
        if (format.Segments.Count == 1 && format.Segments[0].IsLiteral)
            return format.Segments[0].Text;

        Dictionary<string, PropertyInfo>? props = null;
        if (args != null)
            props = _argPropertyCache.GetOrAdd(args.GetType(), BuildPropertyMap);

        var sb = new StringBuilder();
        foreach (var seg in format.Segments)
        {
            if (seg.IsLiteral)
            {
                sb.Append(seg.Text);
                continue;
            }

            if (props != null && props.TryGetValue(seg.Text, out var prop))
            {
                sb.Append(prop.GetValue(args));
            }
            else
            {
                var warnKey = key + "|" + seg.Text;
                if (_warnedMissingParam.TryAdd(warnKey, 0))
                    Log.Warning("Translation key {Key} references missing parameter {Param}", key, seg.Text);
                sb.Append('{').Append(seg.Text).Append('}');
            }
        }
        return sb.ToString();
    }

    private static Dictionary<string, PropertyInfo> BuildPropertyMap(Type type)
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length == 0)
                map[p.Name] = p;
        }
        return map;
    }

    private static CompiledFormat Compile(string template)
    {
        var segments = new List<Segment>();
        var lastEnd = 0;
        foreach (Match m in PlaceholderRegex.Matches(template))
        {
            if (m.Index > lastEnd)
                segments.Add(new Segment(true, template.Substring(lastEnd, m.Index - lastEnd)));
            segments.Add(new Segment(false, m.Groups[1].Value));
            lastEnd = m.Index + m.Length;
        }
        if (lastEnd < template.Length)
            segments.Add(new Segment(true, template.Substring(lastEnd)));
        if (segments.Count == 0)
            segments.Add(new Segment(true, ""));
        return new CompiledFormat(segments);
    }

    private sealed class CompiledFormat
    {
        public List<Segment> Segments { get; }
        public CompiledFormat(List<Segment> segments) { Segments = segments; }
    }

    private readonly struct Segment
    {
        public bool IsLiteral { get; }
        public string Text { get; }
        public Segment(bool isLiteral, string text) { IsLiteral = isLiteral; Text = text; }
    }
}
