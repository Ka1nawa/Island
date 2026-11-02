using System.ComponentModel;
using Island.Core;

namespace Island.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    public MainViewModel(MediaViewModel media, SoundViewModel sound, WeatherViewModel weather,
        SettingsViewModel settings, PlansViewModel plans, SystemViewModel system)
    {
        Media = media;
        Media.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ShowEqualizer));
        Sound = sound;
        System = system;
        Weather = weather;
        Settings = settings;
        Plans = plans;
    }

    public MediaViewModel Media { get; }
    public SoundViewModel Sound { get; }
    public WeatherViewModel Weather { get; }
    public SettingsViewModel Settings { get; }
    public PlansViewModel Plans { get; }
    public SystemViewModel System { get; }

    private string _clock = "";
    public string Clock
    {
        get => _clock;
        set => SetProperty(ref _clock, value);
    }

    private string _dateLine = "";
    public string DateLine
    {
        get => _dateLine;
        set => SetProperty(ref _dateLine, value);
    }

    private int _selectedTab;
    public int SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    private string _timerText = "";
    public string TimerText
    {
        get => _timerText;
        set => SetProperty(ref _timerText, value);
    }

    private bool _hasTimer;
    public bool HasTimer
    {
        get => _hasTimer;
        set
        {
            if (!SetProperty(ref _hasTimer, value)) return;
            OnPropertyChanged(nameof(ShowEqualizer));
        }
    }

    public bool ShowEqualizer => Media.IsPlaying && !HasTimer;

    private string _meetingHint = "";
    public string MeetingHint
    {
        get => _meetingHint;
        set
        {
            if (!SetProperty(ref _meetingHint, value)) return;
            OnPropertyChanged(nameof(HasMeetingHint));
        }
    }

    public bool HasMeetingHint => !string.IsNullOrEmpty(MeetingHint);
}