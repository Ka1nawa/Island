using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Island.Services;

public sealed class City
{
    public string Name { get; init; } = "";
    public string Country { get; init; } = "";
    public string Admin1 { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string TimeZone { get; init; } = "auto";

    public string Subtitle
    {
        get
        {
            var parts = new[] { Admin1, Country }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct();
            return string.Join(" · ", parts);
        }
    }
}

public sealed class HourlyPoint
{
    public DateTime Time { get; init; }
    public double Temperature { get; init; }
    public int Code { get; init; }
    public bool IsDay { get; init; }
}

public sealed class DailyPoint
{
    public DateTime Date { get; init; }
    public int Code { get; init; }
    public double Max { get; init; }
    public double Min { get; init; }
    public int PrecipProbability { get; init; }
}

public sealed class WeatherNow
{
    public double Temperature { get; init; }
    public double FeelsLike { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Wind { get; init; }
    public int Humidity { get; init; }
    public int Code { get; init; }
    public bool IsDay { get; init; }
    public DateTime FetchedAt { get; init; }
    public IReadOnlyList<HourlyPoint> Hours { get; init; } = Array.Empty<HourlyPoint>();
    public IReadOnlyList<DailyPoint> Days { get; init; } = Array.Empty<DailyPoint>();
}

public static class WeatherCodes
{
    public static string Kind(int code, bool isDay) => code switch
    {
        0 => isDay ? "clear" : "clear-night",
        1 or 2 => isDay ? "partly" : "partly-night",
        3 => "cloudy",
        45 or 48 => "fog",
        >= 51 and <= 67 => "rain",
        >= 71 and <= 77 => "snow",
        80 or 81 or 82 => "rain",
        85 or 86 => "snow",
        >= 95 => "storm",
        _ => "cloudy"
    };

    public static string Text(int code) => code switch
    {
        0 => "Ясно",
        1 => "Преимущественно ясно",
        2 => "Переменная облачность",
        3 => "Пасмурно",
        45 or 48 => "Туман",
        51 => "Слабая морось",
        53 => "Морось",
        55 => "Сильная морось",
        56 or 57 => "Ледяная морось",
        61 => "Небольшой дождь",
        63 => "Дождь",
        65 => "Сильный дождь",
        66 or 67 => "Ледяной дождь",
        71 => "Небольшой снег",
        73 => "Снег",
        75 => "Сильный снег",
        77 => "Снежная крупа",
        80 => "Небольшой ливень",
        81 => "Ливень",
        82 => "Сильный ливень",
        85 => "Снегопад",
        86 => "Сильный снегопад",
        95 => "Гроза",
        96 or 99 => "Гроза с градом",
        _ => "Облачно"
    };
}

public sealed class WeatherService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Island/1.0 (Windows widget)");
        return client;
    }

    public async Task<IReadOnlyList<City>> SearchAsync(string query, CancellationToken ct)
    {
        var url = "https://geocoding-api.open-meteo.com/v1/search?name=" +
                  Uri.EscapeDataString(query) + "&count=8&language=ru&format=json";

        using var response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var data = await JsonSerializer.DeserializeAsync<GeocodingResponse>(stream, JsonOptions, ct);

        return data?.Results?.Select(r => new City
        {
            Name = r.Name ?? "",
            Country = r.Country ?? "",
            Admin1 = r.Admin1 ?? "",
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            TimeZone = r.TimeZone ?? "auto"
        }).Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList() ?? new List<City>();
    }

    public async Task<WeatherNow> GetAsync(City city, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(6));
            return await FetchOpenMeteoAsync(city, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return await FetchMetAsync(city, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            return await FetchMetAsync(city, ct);
        }
    }

    private async Task<WeatherNow> FetchOpenMeteoAsync(City city, CancellationToken ct)
    {
        var lat = city.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        var lon = city.Longitude.ToString("0.####", CultureInfo.InvariantCulture);

        var url = "https://api.open-meteo.com/v1/forecast" +
                  "?latitude=" + lat + "&longitude=" + lon +
                  "&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,weather_code,wind_speed_10m" +
                  "&hourly=temperature_2m,weather_code,is_day" +
                  "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
                  "&timezone=auto&forecast_days=6";

        using var response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var data = await JsonSerializer.DeserializeAsync<ForecastResponse>(stream, JsonOptions, ct)
                   ?? throw new InvalidOperationException("empty forecast");

        var current = data.Current ?? throw new InvalidOperationException("no current weather");
        var now = ParseLocal(current.Time);

        var hours = new List<HourlyPoint>();
        var hourly = data.Hourly;
        if (hourly?.Time is { Count: > 0 } times)
        {
            for (var i = 0; i < times.Count; i++)
            {
                var time = ParseLocal(times[i]);
                if (time < now.AddMinutes(-30)) continue;

                hours.Add(new HourlyPoint
                {
                    Time = time,
                    Temperature = hourly.Temperature is { } t && i < t.Count ? t[i] : current.Temperature,
                    Code = hourly.WeatherCode is { } c && i < c.Count ? c[i] : current.WeatherCode,
                    IsDay = hourly.IsDay is { } d && i < d.Count ? d[i] == 1 : true
                });

                if (hours.Count >= 8) break;
            }
        }

        var high = current.Temperature;
        var low = current.Temperature;
        if (data.Daily?.TemperatureMax is { Count: > 0 } max && max.Count > 0) high = max[0];
        if (data.Daily?.TemperatureMin is { Count: > 0 } min && min.Count > 0) low = min[0];

        var days = new List<DailyPoint>();
        if (data.Daily?.Time is { Count: > 0 } dailyTimes)
        {
            for (var i = 0; i < dailyTimes.Count; i++)
            {
                var date = ParseLocal(dailyTimes[i]);
                days.Add(new DailyPoint
                {
                    Date = date.Date,
                    Code = data.Daily.WeatherCode is { } codes && i < codes.Count ? codes[i] : current.WeatherCode,
                    Max = data.Daily.TemperatureMax is { } maxes && i < maxes.Count ? maxes[i] : current.Temperature,
                    Min = data.Daily.TemperatureMin is { } mins && i < mins.Count ? mins[i] : current.Temperature,
                    PrecipProbability = data.Daily.Precipitation is { } probs && i < probs.Count ? probs[i] : 0
                });
            }
        }

        if (days.Count > 0)
        {
            high = days[0].Max;
            low = days[0].Min;
        }

        return new WeatherNow
        {
            Temperature = current.Temperature,
            FeelsLike = current.FeelsLike,
            High = high,
            Low = low,
            Wind = current.WindSpeed,
            Humidity = current.Humidity,
            Code = current.WeatherCode,
            IsDay = current.IsDay == 1,
            FetchedAt = DateTime.Now,
            Hours = hours,
            Days = days
        };
    }

    /// <summary>MET Norway fallback: used when api.open-meteo.com is unreachable (blocked network, outage).</summary>
    private async Task<WeatherNow> FetchMetAsync(City city, CancellationToken ct)
    {
        var lat = city.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        var lon = city.Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        var url = "https://api.met.no/weatherapi/locationforecast/2.0/compact?lat=" + lat + "&lon=" + lon;

        using var response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var data = await JsonSerializer.DeserializeAsync<MetResponse>(stream, JsonOptions, ct)
                   ?? throw new InvalidOperationException("empty met forecast");

        var entries = data.Properties?.Timeseries;
        if (entries is null || entries.Count == 0) throw new InvalidOperationException("no met timeseries");

        var points = new List<MetPoint>();
        foreach (var entry in entries)
        {
            var details = entry.Data?.Instant?.Details;
            if (details?.Temperature is null || entry.Time is null) continue;

            if (!DateTimeOffset.TryParse(entry.Time, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var parsed)) continue;

            var period = entry.Data!.Next1 ?? entry.Data.Next6;
            points.Add(new MetPoint
            {
                Time = parsed.ToLocalTime(),
                Temperature = details.Temperature.Value,
                Humidity = (int)Math.Round(details.Humidity ?? 0),
                Wind = (details.Wind ?? 0) * 3.6,
                Symbol = period?.Summary?.Symbol,
                Probability = (int)Math.Round(entry.Data.Next6?.Details?.Probability
                                              ?? entry.Data.Next1?.Details?.Probability ?? 0)
            });
        }

        if (points.Count == 0) throw new InvalidOperationException("no usable met points");

        var nowIndex = 0;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Time <= DateTimeOffset.Now) nowIndex = i;
            else break;
        }

        var current = points[nowIndex];
        var isDay = IsDaySymbol(current.Symbol, current.Time);

        var hours = points.Skip(nowIndex).Take(8).Select(point => new HourlyPoint
        {
            Time = point.Time.DateTime,
            Temperature = point.Temperature,
            Code = MetCode(point.Symbol),
            IsDay = IsDaySymbol(point.Symbol, point.Time)
        }).ToList();

        var days = new List<DailyPoint>();
        foreach (var group in points.GroupBy(point => point.Time.Date).Take(6))
        {
            var noon = group.OrderBy(point => Math.Abs(point.Time.Hour - 12)).First();
            days.Add(new DailyPoint
            {
                Date = group.Key,
                Code = MetCode(noon.Symbol),
                Max = Math.Round(group.Max(point => point.Temperature)),
                Min = Math.Round(group.Min(point => point.Temperature)),
                PrecipProbability = group.Max(point => point.Probability)
            });
        }

        var today = days.FirstOrDefault();
        return new WeatherNow
        {
            Temperature = Math.Round(current.Temperature),
            FeelsLike = Math.Round(current.Temperature),
            High = today?.Max ?? Math.Round(current.Temperature),
            Low = today?.Min ?? Math.Round(current.Temperature),
            Wind = Math.Round(current.Wind),
            Humidity = current.Humidity,
            Code = MetCode(current.Symbol),
            IsDay = isDay,
            FetchedAt = DateTime.Now,
            Hours = hours,
            Days = days
        };
    }

    private static bool IsDaySymbol(string? symbol, DateTimeOffset time)
    {
        if (symbol?.Contains("_night", StringComparison.OrdinalIgnoreCase) == true) return false;
        if (symbol?.Contains("_day", StringComparison.OrdinalIgnoreCase) == true) return true;
        return time.Hour is >= 6 and < 19;
    }

    private static int MetCode(string? symbol)
    {
        var s = (symbol ?? "").ToLowerInvariant();
        if (s.Contains("thunder")) return 95;
        if (s.Contains("heavyrain") && s.Contains("shower")) return 82;
        if (s.Contains("lightrain") && s.Contains("shower")) return 80;
        if (s.Contains("rainshowers")) return 81;
        if (s.Contains("heavyrain")) return 65;
        if (s.Contains("lightrain")) return 61;
        if (s.Contains("rain")) return 63;
        if (s.Contains("heavysnow")) return 75;
        if (s.Contains("lightsnow")) return 71;
        if (s.Contains("snowshowers")) return 85;
        if (s.Contains("snow")) return 73;
        if (s.Contains("sleet")) return 66;
        if (s.Contains("fog")) return 45;
        if (s.Contains("partlycloudy")) return 2;
        if (s.Contains("cloudy")) return 3;
        if (s.Contains("fair")) return 1;
        if (s.Contains("clear")) return 0;
        return 3;
    }

    private sealed class MetPoint
    {
        public DateTimeOffset Time { get; init; }
        public double Temperature { get; init; }
        public int Humidity { get; init; }
        public double Wind { get; init; }
        public string? Symbol { get; init; }
        public int Probability { get; init; }
    }

    private static DateTime ParseLocal(string? value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed)) return parsed;
        return DateTime.Now;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private sealed class MetResponse
    {
        [JsonPropertyName("properties")] public MetProperties? Properties { get; set; }
    }

    private sealed class MetProperties
    {
        [JsonPropertyName("timeseries")] public List<MetEntry>? Timeseries { get; set; }
    }

    private sealed class MetEntry
    {
        [JsonPropertyName("time")] public string? Time { get; set; }
        [JsonPropertyName("data")] public MetData? Data { get; set; }
    }

    private sealed class MetData
    {
        [JsonPropertyName("instant")] public MetInstant? Instant { get; set; }
        [JsonPropertyName("next_1_hours")] public MetPeriod? Next1 { get; set; }
        [JsonPropertyName("next_6_hours")] public MetPeriod? Next6 { get; set; }
    }

    private sealed class MetInstant
    {
        [JsonPropertyName("details")] public MetDetails? Details { get; set; }
    }

    private sealed class MetPeriod
    {
        [JsonPropertyName("summary")] public MetSummary? Summary { get; set; }
        [JsonPropertyName("details")] public MetDetails? Details { get; set; }
    }

    private sealed class MetSummary
    {
        [JsonPropertyName("symbol_code")] public string? Symbol { get; set; }
    }

    private sealed class MetDetails
    {
        [JsonPropertyName("air_temperature")] public double? Temperature { get; set; }
        [JsonPropertyName("relative_humidity")] public double? Humidity { get; set; }
        [JsonPropertyName("wind_speed")] public double? Wind { get; set; }
        [JsonPropertyName("probability_of_precipitation")] public double? Probability { get; set; }
    }

    private sealed class GeocodingResponse
    {
        [JsonPropertyName("results")] public List<GeoResult>? Results { get; set; }
    }

    private sealed class GeoResult
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("country")] public string? Country { get; set; }
        [JsonPropertyName("admin1")] public string? Admin1 { get; set; }
        [JsonPropertyName("latitude")] public double Latitude { get; set; }
        [JsonPropertyName("longitude")] public double Longitude { get; set; }
        [JsonPropertyName("timezone")] public string? TimeZone { get; set; }
    }

    private sealed class ForecastResponse
    {
        [JsonPropertyName("current")] public CurrentBlock? Current { get; set; }
        [JsonPropertyName("hourly")] public HourlyBlock? Hourly { get; set; }
        [JsonPropertyName("daily")] public DailyBlock? Daily { get; set; }
    }

    private sealed class CurrentBlock
    {
        [JsonPropertyName("time")] public string? Time { get; set; }
        [JsonPropertyName("temperature_2m")] public double Temperature { get; set; }
        [JsonPropertyName("apparent_temperature")] public double FeelsLike { get; set; }
        [JsonPropertyName("relative_humidity_2m")] public int Humidity { get; set; }
        [JsonPropertyName("is_day")] public int IsDay { get; set; }
        [JsonPropertyName("weather_code")] public int WeatherCode { get; set; }
        [JsonPropertyName("wind_speed_10m")] public double WindSpeed { get; set; }
    }

    private sealed class HourlyBlock
    {
        [JsonPropertyName("time")] public List<string>? Time { get; set; }
        [JsonPropertyName("temperature_2m")] public List<double>? Temperature { get; set; }
        [JsonPropertyName("weather_code")] public List<int>? WeatherCode { get; set; }
        [JsonPropertyName("is_day")] public List<int>? IsDay { get; set; }
    }

    private sealed class DailyBlock
    {
        [JsonPropertyName("time")] public List<string>? Time { get; set; }
        [JsonPropertyName("weather_code")] public List<int>? WeatherCode { get; set; }
        [JsonPropertyName("temperature_2m_max")] public List<double>? TemperatureMax { get; set; }
        [JsonPropertyName("temperature_2m_min")] public List<double>? TemperatureMin { get; set; }
        [JsonPropertyName("precipitation_probability_max")] public List<int>? Precipitation { get; set; }
    }
}
