using System.Windows;
using System.Windows.Controls;
using Island.ViewModels;

namespace Island.Views;

public partial class SoundPage : UserControl
{
    public SoundPage() => InitializeComponent();

    private MainViewModel Vm => (MainViewModel)DataContext;

    private void ApplyGame_Click(object sender, RoutedEventArgs e) => Vm.Sound.ApplyPreset("game");

    private void SaveGame_Click(object sender, RoutedEventArgs e) => Vm.Sound.SavePreset("game");

    private void ApplyWork_Click(object sender, RoutedEventArgs e) => Vm.Sound.ApplyPreset("work");

    private void SaveWork_Click(object sender, RoutedEventArgs e) => Vm.Sound.SavePreset("work");

    private void AppMute_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is AudioAppViewModel app)
            app.Muted = !app.Muted;
    }

    private void MasterMute_Click(object sender, RoutedEventArgs e) => Vm.Sound.MasterMuted = !Vm.Sound.MasterMuted;
}
