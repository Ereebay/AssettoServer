namespace AssettoServer.Shared.Services;

public interface ILocalizationService
{
    string Get(string key, object? args = null);

    /// <summary>
    /// Returns the raw (un-rendered) template for the active locale, with placeholders like {name} left intact.
    /// Used when the substitution happens elsewhere (e.g. injecting templates into a client-side Lua script).
    /// </summary>
    string GetRaw(string key);

    void RegisterSource(string sourceDir, string @namespace);
}
