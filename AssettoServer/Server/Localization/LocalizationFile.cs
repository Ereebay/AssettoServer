using System.Collections.Generic;

namespace AssettoServer.Server.Localization;

public class LocalizationFile
{
    public string Locale { get; set; } = "";
    public string? Fallback { get; set; }
    public Dictionary<string, string> Strings { get; set; } = new();
}
