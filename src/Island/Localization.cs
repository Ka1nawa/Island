using System;
using System.Globalization;
using System.Windows;

namespace Island;

public static class L
{
    private static string _current = "ru";

    public static event Action? LanguageChanged;

    public static string Current => _current;

    public static CultureInfo Culture => CultureInfo.GetCultureInfo(_current == "ru" ? "ru-RU" : "en-US");

    public static void Apply(string language)
    {
        var lang = language == "en" ? "en" : "ru";
        if (lang == _current && Application.Current.Resources.MergedDictionaries.Count > 0)
        {
            // still make sure the dictionary is present on first run
            if (FindStringsDictionary() is not null) return;
        }

        var app = Application.Current;
        var merged = app.Resources.MergedDictionaries;
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Source?.OriginalString.Contains("Strings.") == true) merged.RemoveAt(i);
        }

        merged.Add(new ResourceDictionary
        {
            Source = new Uri($"Resources/Strings.{lang}.xaml", UriKind.Relative)
        });

        _current = lang;
        LanguageChanged?.Invoke();
    }

    private static ResourceDictionary? FindStringsDictionary()
    {
        foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
        {
            if (dictionary.Source?.OriginalString.Contains("Strings.") == true) return dictionary;
        }
        return null;
    }

    public static string T(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    public static string F(string key, params object[] args)
        => string.Format(Culture, T(key), args);

    public static string DateLine(DateTime now)
        => now.ToString("ddd, d MMM", Culture);

    public static string AppsCount(int count)
    {
        if (_current == "en")
            return F(count == 1 ? "AppsOne" : "AppsMany", count);

        var tail = count % 100;
        if (tail is >= 11 and <= 14) return F("AppsMany", count);
        return (count % 10) switch
        {
            1 => F("AppsOne", count),
            2 or 3 or 4 => F("AppsFew", count),
            _ => F("AppsMany", count)
        };
    }

    public static string WeatherKey(int code) => code switch
    {
        0 => "WmoClear",
        1 => "WmoMostlyClear",
        2 => "WmoPartly",
        3 => "WmoCloudy",
        45 or 48 => "WmoFog",
        >= 51 and <= 57 => "WmoDrizzle",
        >= 61 and <= 65 => "WmoRain",
        66 or 67 => "WmoHeavyRain",
        71 or 73 or 75 or 77 => "WmoSnow",
        80 or 81 or 82 => "WmoShowers",
        85 or 86 => "WmoSnow",
        95 => "WmoThunder",
        96 or 99 => "WmoHail",
        _ => "WmoCloudy"
    };
}
