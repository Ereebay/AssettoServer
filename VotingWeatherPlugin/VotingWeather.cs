using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Services;
using AssettoServer.Shared.Weather;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace VotingWeatherPlugin;

public class VotingWeather : BackgroundService
{
    private readonly WeatherManager _weatherManager;
    private readonly IWeatherTypeProvider _weatherTypeProvider;
    private readonly EntryCarManager _entryCarManager;
    private readonly VotingWeatherConfiguration _configuration;
    private readonly ILocalizationService _l10n;
    private readonly List<WeatherFxType> _weathers;
    private readonly List<ACTcpClient> _alreadyVoted = [];
    private readonly List<WeatherChoice> _availableWeathers = [];

    private bool _votingOpen = false;

    private class WeatherChoice
    {
        public WeatherFxType Weather { get; init; }
        public int Votes { get; set; }
    }

    public VotingWeather(VotingWeatherConfiguration configuration,
        WeatherManager weatherManager,
        IWeatherTypeProvider weatherTypeProvider,
        EntryCarManager entryCarManager,
        ILocalizationService l10n)
    {
        _configuration = configuration;
        _weatherManager = weatherManager;
        _weatherTypeProvider = weatherTypeProvider;
        _entryCarManager = entryCarManager;
        _l10n = l10n;

        if (!_configuration.BlacklistedWeathers.Contains(WeatherFxType.None))
        {
            _configuration.BlacklistedWeathers.Add(WeatherFxType.None);
        }

        _weathers = Enum.GetValues<WeatherFxType>().Except(_configuration.BlacklistedWeathers).ToList();

        var pluginDir = Path.GetDirectoryName(typeof(VotingWeather).Assembly.Location)!;
        _l10n.RegisterSource(Path.Combine(pluginDir, "lang"), "voteweather");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateAsync(stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during voting weather update");
            }
            finally
            {
                await Task.Delay(_configuration.VotingIntervalMilliseconds - _configuration.VotingDurationMilliseconds, stoppingToken);
            }
        }
    }

    internal void CountVote(ACTcpClient client, int choice)
    {
        if (!_votingOpen)
        {
            client.SendChatMessage(_l10n.Get("plugin.voteweather.cmd.no_vote"));
            return;
        }

        if (choice >= _availableWeathers.Count || choice < 0)
        {
            client.SendChatMessage(_l10n.Get("plugin.voteweather.cmd.invalid_choice"));
            return;
        }

        if (_alreadyVoted.Contains(client))
        {
            client.SendChatMessage(_l10n.Get("plugin.voteweather.cmd.already_voted"));
            return;
        }

        _alreadyVoted.Add(client);

        var votedWeather = _availableWeathers[choice];
        votedWeather.Votes++;

        client.SendChatMessage(_l10n.Get("plugin.voteweather.cmd.vote_counted", new { weather = votedWeather.Weather }));
    }

    private async Task UpdateAsync(CancellationToken stoppingToken)
    {
        var last = _weatherManager.CurrentWeather;

        _availableWeathers.Clear();
        _alreadyVoted.Clear();

        var weathersLeft = new List<WeatherFxType>(_weathers);

        _entryCarManager.BroadcastChat(_l10n.Get("plugin.voteweather.broadcast.header"));
        for (int i = 0; i < _configuration.NumChoices; i++)
        {
            if (weathersLeft.Count < 1) break;
            
            var nextWeather = weathersLeft[Random.Shared.Next(weathersLeft.Count)];
            _availableWeathers.Add(new WeatherChoice { Weather = nextWeather, Votes = 0 });
            weathersLeft.Remove(nextWeather);

            _entryCarManager.BroadcastChat(_l10n.Get("plugin.voteweather.broadcast.option", new { index = i, weather = nextWeather }));
        }

        _votingOpen = true;
        await Task.Delay(_configuration.VotingDurationMilliseconds, stoppingToken);
        _votingOpen = false;

        int maxVotes = _availableWeathers.Max(w => w.Votes);
        var weathers = _availableWeathers.Where(w => w.Votes == maxVotes).Select(w => w.Weather).ToList();

        var winner = weathers[Random.Shared.Next(weathers.Count)];
        var winnerType = _weatherTypeProvider.GetWeatherType(winner);

        if (maxVotes == 0 && _configuration.KeepWeatherOnNoVotes)
        {
            _entryCarManager.BroadcastChat(_l10n.Get("plugin.voteweather.no_votes"));
            return;
        }
        
        _entryCarManager.BroadcastChat(_l10n.Get("plugin.voteweather.broadcast.result", new { weather = winner }));

        _weatherManager.SetWeather(new WeatherData(last.Type, winnerType)
        {
            TransitionDuration = 120000.0,
            TemperatureAmbient = last.TemperatureAmbient,
            TemperatureRoad = (float)WeatherUtils.GetRoadTemperature(_weatherManager.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0, last.TemperatureAmbient,
                winnerType.TemperatureCoefficient),
            Pressure = last.Pressure,
            Humidity = winnerType.Humidity,
            WindSpeed = last.WindSpeed,
            WindDirection = last.WindDirection,
            RainIntensity = last.RainIntensity,
            RainWetness = last.RainWetness,
            RainWater = last.RainWater,
            TrackGrip = last.TrackGrip
        });
    }
}
