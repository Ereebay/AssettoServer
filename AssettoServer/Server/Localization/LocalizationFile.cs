using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Localization;

public class LocalizationFile
{
    [YamlMember(Alias = "locale")]
    public string Locale { get; set; } = "";

    [YamlMember(Alias = "fallback")]
    public string? Fallback { get; set; }

    [YamlMember(Alias = "strings")]
    public Dictionary<string, string> Strings { get; set; } = new();
}
