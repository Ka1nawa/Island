using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Island.ViewModels;

namespace Island.Views;

public partial class WeatherPage : UserControl
{
    public WeatherPage() => InitializeComponent();

    private MainViewModel Vm => (MainViewModel)DataContext;

    private async void RefreshWeather_Click(object sender, RoutedEventArgs e) => await Vm.Weather.LoadAsync();

    private void OpenSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Text = string.Empty;
        Vm.Weather.BeginSearch();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SearchInput.Focus();
            Keyboard.Focus(SearchInput);
        }), DispatcherPriority.Input);
    }

    private void CancelSearch_Click(object sender, RoutedEventArgs e) => Vm.Weather.CancelSearch();

    private async void PrevCity_Click(object sender, RoutedEventArgs e) => await Vm.Weather.PrevCityAsync();

    private async void NextCity_Click(object sender, RoutedEventArgs e) => await Vm.Weather.NextCityAsync();

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = Vm.Weather.SearchAsync(SearchInput.Text);
    }

    private async void City_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is CityResultViewModel result)
            await Vm.Weather.SelectCityAsync(result.City);
    }

    private async void SearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Vm.Weather.Results.Count > 0)
        {
            e.Handled = true;
            await Vm.Weather.SelectCityAsync(Vm.Weather.Results[0].City);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Vm.Weather.CancelSearch();
        }
    }
}