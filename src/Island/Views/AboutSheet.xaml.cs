using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Island.Views;

public partial class AboutSheet : UserControl
{
    public AboutSheet()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var text = version is null ? "0.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        VersionText.Text = L.F("AboutVersion", text);
    }

    private void OpenSite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://open-meteo.com/") { UseShellExecute = true });
        }
        catch
        {
        }
    }
}