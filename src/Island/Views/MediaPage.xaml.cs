using System.Windows;
using System.Windows.Controls;
using Island.Controls;
using Island.ViewModels;

namespace Island.Views;

public partial class MediaPage : UserControl
{
    public MediaPage() => InitializeComponent();

    public VisualizerControl VisualizerControl => Visualizer;

    private MainViewModel Vm => (MainViewModel)DataContext;

    private async void Play_Click(object sender, RoutedEventArgs e) => await Vm.Media.TogglePlayPauseAsync();

    private async void Next_Click(object sender, RoutedEventArgs e) => await Vm.Media.NextAsync();

    private async void Prev_Click(object sender, RoutedEventArgs e) => await Vm.Media.PreviousAsync();

    private void MasterMute_Click(object sender, RoutedEventArgs e) => Vm.Sound.MasterMuted = !Vm.Sound.MasterMuted;

    private void Lyrics_Click(object sender, RoutedEventArgs e) => Vm.Media.IsLyricsMode = !Vm.Media.IsLyricsMode;
}
