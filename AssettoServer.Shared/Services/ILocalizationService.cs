namespace AssettoServer.Shared.Services;

public interface ILocalizationService
{
    string Get(string key, object? args = null);
    void RegisterSource(string sourceDir, string @namespace);
}
