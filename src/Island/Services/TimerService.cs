using System;
using System.Windows.Threading;

namespace Island.Services;

/// <summary>Single countdown timer shown in the pill (mini-activity).</summary>
public sealed class TimerService : IDisposable
{
    private readonly DispatcherTimer _ticker;
    private DateTime _endsAt;
    private bool _running;

    public TimerService()
    {
        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _ticker.Tick += (_, _) => Tick();
    }

    public event Action<TimeSpan>? RemainingChanged;
    public event Action? Finished;

    public bool IsRunning => _running;
    public TimeSpan Total { get; private set; }
    public TimeSpan Remaining => _running ? _endsAt - DateTime.UtcNow : TimeSpan.Zero;

    public void Start(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return;

        Total = duration;
        _endsAt = DateTime.UtcNow + duration;
        _running = true;
        _ticker.Start();
        RemainingChanged?.Invoke(Remaining);
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _ticker.Stop();
        RemainingChanged?.Invoke(TimeSpan.Zero);
    }

    private void Tick()
    {
        var remaining = _endsAt - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _running = false;
            _ticker.Stop();
            RemainingChanged?.Invoke(TimeSpan.Zero);
            Finished?.Invoke();
            return;
        }

        RemainingChanged?.Invoke(remaining);
    }

    public void Dispose() => _ticker.Stop();
}
