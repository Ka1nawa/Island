using System;
using System.Windows;

namespace Island;

public static class Theme
{
    private static string _current = "dark";
    private static string _mode = "dark";

    public static event Action? Changed;

    public static string Current => _current;

    public static string Mode => _mode;

    /// <summary>Resolves the mode to a concrete theme: dark, light or auto (system).</summary>
    public static void Apply(string? theme)
    {
        var mode = theme == "light" ? "light" : theme == "auto" ? "auto" : "dark";
        var name = mode == "light" ? "light" : mode == "auto" ? ResolveSystem() : "dark";

        _mode = mode;
        _current = name;

        var app = Application.Current;
        if (app is null) return;

        var merged = app.Resources.MergedDictionaries;
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Source?.OriginalString.Contains("Theme.") == true) merged.RemoveAt(i);
        }

        merged.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"Resources/Theme.{name}.xaml", UriKind.Relative)
        });

        Changed?.Invoke();
    }

    public static string ResolveSystem()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return ResolveMode(value is int number && number != 0);
        }
        catch
        {
            return "dark";
        }
    }

    public static string ResolveMode(bool appsUseLightTheme) => appsUseLightTheme ? "light" : "dark";
}
