using System;
using AssettoServer.Shared.Services;

namespace AssettoServer.Shared.Weather;

public static class WeatherFxTypeExtensions
{
    /// <summary>
    /// Returns the localized display name for a weather type (catalog key "weather.&lt;value&gt;"),
    /// falling back to the raw enum name when no translation is defined.
    /// </summary>
    public static string Localized(this WeatherFxType type, ILocalizationService localization)
    {
        var name = localization.Get($"weather.{type}");
        return name.StartsWith("[missing:", StringComparison.Ordinal) ? type.ToString() : name;
    }
}
