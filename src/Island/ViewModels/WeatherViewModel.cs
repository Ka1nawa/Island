using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Island.Core;
using Island.Services;

namespace Island.ViewModels;

public sealed class HourViewModel
{
    public HourViewModel(HourlyPoint point, bool isNow)
    {
        TimeText = isNow ? L.T("Now") : point.Time.ToString("HH:mm");
        TempText = Math.Round(point.Temperature).ToString("0") + "°";
        Kind = WeatherCodes.Kind(point.Code, point.IsDay);
        IsNow = isNow;
    }

    public string TimeText { get; }
    public string TempText { get; }
    public string Kind { get; }
    public bool IsNow { get; }
}

public sealed class DayViewModel
{
    public DayViewModel(DailyPoint point, int index)
    {
        DayText = index == 0 ? L.T("Today") : point.Date.ToString("ddd", L.Culture);
        Kind = WeatherCodes.Kind(point.Code, true);
        TempText = Math.Round(point.Max).ToString("0") + "° / " + Math.Round(point.Min).ToString("0") + "°";
        PrecipText = point.PrecipProbability > 0 ? point.PrecipProbability + "%" : "";
        HasPrecip = point.PrecipProbability > 0;
    }

    public string DayText { get; }
    public string Kind { get; }
    public string TempText { get; }
    public string PrecipText { get; }
    public bool HasPrecip { get; }
}

public sealed class CityResultViewModel
{
    public CityResultViewModel(City city) => City = city;
    public City City { get; }
    public string Name => City.Name;
    public string Subtitle => City.Subtitle;
}

public sealed class WeatherViewModel : ObservableObject
{
    private readonly WeatherService _service;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _loadCts;

    private City _city;

    public WeatherViewModel(WeatherService service, AppSettings settings)
    {
        _service = service;
        _settings = settings;
        _city = new City
        {
            Name = settings.CityName,
            Country = settings.CityCountry,
            Latitude = settings.Latitude,
            Longitude = settings.Longitude,
            TimeZone = settings.TimeZone
        };

        CityName = settings.CityName;
        CitySubtitle = settings.CityCountry;

        if (settings.Cities.Count == 0)
        {
            settings.Cities.Add(new CityEntry
            {
                Name = settings.CityName,
                Country = settings.CityCountry,
                Latitude = settings.Latitude,
                Longitude = settings.Longitude,
                TimeZone = settings.TimeZone
            });
        }

        if (settings.Weather is { } cached) ApplyCache(cached);
    }

    public ObservableCollection<HourViewModel> Hours { get; } = new();
    public ObservableCollection<DayViewModel> Days { get; } = new();
    public ObservableCollection<CityResultViewModel> Results { get; } = new();

    public string CityName { get; private set; }
    public string CitySubtitle { get; private set; }
    public bool HasWeather { get; private set; }

    private WeatherNow? _lastNow;
    public bool HasHours => Hours.Count > 0;

    public string TemperatureText { get; private set; } = "—";
    public string FeelsText { get; private set; } = "";
    public string ConditionText { get; private set; } = "";
    public string HighLowText { get; private set; } = "";
    public string WindText { get; private set; } = "—";
    public string HumidityText { get; private set; } = "—";
    public string IconKind { get; private set; } = "cloudy";
    public string UpdatedText { get; private set; } = "";
    public string WarningText { get; private set; } = "";
    public bool HasWarning => !string.IsNullOrEmpty(WarningText);
    public bool CanSwitchCity => _settings.Cities.Count > 1;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }
    private bool _isLoading;

    public bool IsSearching
    {
        get => _isSearching;
        set
        {
            if (!SetProperty(ref _isSearching, value)) return;
            OnPropertyChanged(nameof(ShowHint));
        }
    }
    private bool _isSearching;

    public bool HasResults => Results.Count > 0;

    public bool ShowNoResults =>
        IsSearching && Results.Count == 0 && SearchQuery.Trim().Length >= 2;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!SetProperty(ref _searchQuery, value)) return;
            OnPropertyChanged(nameof(ShowHint));
            OnPropertyChanged(nameof(ShowNoResults));
        }
    }
    private string _searchQuery = "";

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }
    private string _errorText = "";

    public bool HasError => !string.IsNullOrEmpty(ErrorText);
    public bool ShowHint => IsSearching && Results.Count == 0 && SearchQuery.Length < 2;

    public async Task LoadAsync()
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        IsLoading = true;

        try
        {
            var weather = await _service.GetAsync(_city, cts.Token);
            if (cts.IsCancellationRequested) return;
            Apply(weather);
            ErrorText = "";
            Cache(weather);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (HasWeather)
                ErrorText = L.T("WeatherError");
            else
                ErrorText = L.T("NoConnection");
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts)) IsLoading = false;
        }
    }

    public async Task SearchAsync(string query)
    {
        _searchCts?.Cancel();
        SearchQuery = query;

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            Results.Clear();
            NotifyResults();
            return;
        }

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        NotifyResults();

        try
        {
            await Task.Delay(320, cts.Token);
            var found = await _service.SearchAsync(query.Trim(), cts.Token);
            if (cts.IsCancellationRequested) return;

            Results.Clear();
            foreach (var city in found) Results.Add(new CityResultViewModel(city));
            NotifyResults();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            Results.Clear();
            NotifyResults();
        }
    }

    public Task NextCityAsync() => SwitchCityAsync(1);

    public Task PrevCityAsync() => SwitchCityAsync(-1);

    private async Task SwitchCityAsync(int delta)
    {
        if (_settings.Cities.Count < 2) return;

        var index = _settings.Cities.FindIndex(c =>
            c.Name == _city.Name && Math.Abs(c.Latitude - _city.Latitude) < 0.001);
        if (index < 0) index = 0;

        var next = (index + delta + _settings.Cities.Count) % _settings.Cities.Count;
        var entry = _settings.Cities[next];

        await SelectCityAsync(new City
        {
            Name = entry.Name,
            Country = entry.Country,
            Latitude = entry.Latitude,
            Longitude = entry.Longitude,
            TimeZone = entry.TimeZone
        }, remember: false);
    }

    public async Task SelectCityAsync(City city, bool remember = true)
    {
        if (remember) Remember(city);

        _city = city;
        CityName = city.Name;
        CitySubtitle = city.Country;
        OnPropertyChanged(nameof(CityName));
        OnPropertyChanged(nameof(CitySubtitle));

        _settings.CityName = city.Name;
        _settings.CityCountry = city.Country;
        _settings.Latitude = city.Latitude;
        _settings.Longitude = city.Longitude;
        _settings.TimeZone = city.TimeZone;
        SettingsStore.Save(_settings);

        IsSearching = false;
        SearchQuery = "";
        Results.Clear();
        NotifyResults();

        await LoadAsync();
    }

    private void Remember(City city)
    {
        var existing = _settings.Cities.FirstOrDefault(c =>
            c.Name == city.Name && Math.Abs(c.Latitude - city.Latitude) < 0.001);

        if (existing is null)
        {
            _settings.Cities.Add(new CityEntry
            {
                Name = city.Name,
                Country = city.Country,
                Latitude = city.Latitude,
                Longitude = city.Longitude,
                TimeZone = city.TimeZone
            });
        }
        else
        {
            existing.Country = city.Country;
            existing.TimeZone = city.TimeZone;
        }

        OnPropertyChanged(nameof(CanSwitchCity));
    }

    public void RefreshLanguage()
    {
        if (_lastNow is not null) Apply(_lastNow);
        else if (_settings.Weather is { } cache) ApplyCache(cache);
    }

    public void BeginSearch()
    {
        SearchQuery = "";
        Results.Clear();
        NotifyResults();
        IsSearching = true;
    }

    public void CancelSearch()
    {
        _searchCts?.Cancel();
        IsSearching = false;
        SearchQuery = "";
        Results.Clear();
        NotifyResults();
    }

    private void NotifyResults()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowHint));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    private void Apply(WeatherNow weather)
    {
        HasWeather = true;
        TemperatureText = Math.Round(weather.Temperature).ToString("0") + "°";
        FeelsText = L.F("FeelsLike", Math.Round(weather.FeelsLike).ToString("0") + "°");
        ConditionText = L.T(L.WeatherKey(weather.Code));
        HighLowText = L.F("HighLow", Math.Round(weather.High).ToString("0") + "°", Math.Round(weather.Low).ToString("0") + "°");
        WindText = Math.Round(weather.Wind).ToString("0") + " " + L.T("Kmh");
        HumidityText = weather.Humidity + "%";
        IconKind = WeatherCodes.Kind(weather.Code, weather.IsDay);
        UpdatedText = L.F("UpdatedAt", weather.FetchedAt.ToString("HH:mm"));
        _lastNow = weather;

        Hours.Clear();
        for (var i = 0; i < weather.Hours.Count; i++)
            Hours.Add(new HourViewModel(weather.Hours[i], i == 0));

        Days.Clear();
        for (var i = 0; i < weather.Days.Count && i < 5; i++)
            Days.Add(new DayViewModel(weather.Days[i], i));

        WarningText = WarningFor(weather);
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(WarningText));

        OnPropertyChanged(string.Empty);
    }

    public static string WarningFor(WeatherNow weather)
    {
        if (weather.Code >= 95) return L.T("WarnStorm");
        if (weather.Wind >= 40) return L.T("WarnWind");
        if (weather.Code is 65 or 82) return L.T("WarnHeavyRain");
        return "";
    }

    private void ApplyCache(WeatherCache cache)
    {
        HasWeather = true;
        TemperatureText = Math.Round(cache.Temperature).ToString("0") + "°";
        FeelsText = L.F("FeelsLike", Math.Round(cache.FeelsLike).ToString("0") + "°");
        ConditionText = L.T(L.WeatherKey(cache.Code));
        HighLowText = L.F("HighLow", Math.Round(cache.High).ToString("0") + "°", Math.Round(cache.Low).ToString("0") + "°");
        WindText = Math.Round(cache.Wind).ToString("0") + " " + L.T("Kmh");
        HumidityText = cache.Humidity + "%";
        IconKind = WeatherCodes.Kind(cache.Code, true);
        UpdatedText = L.T("FromCache");

        Days.Clear();
        for (var i = 0; i < cache.Daily.Count && i < 5; i++)
        {
            var day = cache.Daily[i];
            if (!DateTime.TryParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)) continue;

            Days.Add(new DayViewModel(new DailyPoint
            {
                Date = date,
                Code = day.Code,
                Max = day.Max,
                Min = day.Min,
                PrecipProbability = day.Precip
            }, i));
        }
    }

    private void Cache(WeatherNow weather)
    {
        _settings.Weather = new WeatherCache
        {
            Temperature = weather.Temperature,
            FeelsLike = weather.FeelsLike,
            High = weather.High,
            Low = weather.Low,
            Code = weather.Code,
            Humidity = weather.Humidity,
            Wind = weather.Wind,
            SavedAt = DateTime.Now.ToString("o"),
            Daily = weather.Days.Select(d => new DailyCache
            {
                Date = d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Code = d.Code,
                Max = d.Max,
                Min = d.Min,
                Precip = d.PrecipProbability
            }).ToList()
        };
        SettingsStore.Save(_settings);
    }
}