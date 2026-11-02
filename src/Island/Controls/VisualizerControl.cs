using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Island.Services;

namespace Island.Controls;

/// <summary>
/// A slim spectrum of rounded bars pulled from the system loopback capture.
/// </summary>
public sealed class VisualizerControl : FrameworkElement
{
    private const int Count = 28;

    private readonly float[] _display = new float[Count];
    private readonly DispatcherTimer _timer;

    private static readonly LinearGradientBrush BarBrush = CreateBrush();

    public VisualizerControl()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Advance();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    public SpectrumService? Source { get; set; }

    private static LinearGradientBrush CreateBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 1),
            EndPoint = new Point(0, 0)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(230, 255, 255, 255), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(190, 155, 190, 255), 0.6));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(120, 110, 160, 255), 1));
        brush.Freeze();
        return brush;
    }

    private void Advance()
    {
        var bands = Source?.Bands();
        for (var i = 0; i < Count; i++)
        {
            var target = bands is not null && i < bands.Length ? bands[i] : 0f;
            _display[i] += (target - _display[i]) * (target > _display[i] ? 0.5f : 0.14f);
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 1 || height <= 1) return;

        var gap = 3.0;
        var barWidth = Math.Max(1.6, (width - gap * (Count - 1)) / Count);
        var radius = barWidth / 2;
        var allIdle = true;
        for (var i = 0; i < Count; i++)
        {
            if (_display[i] > 0.02f)
            {
                allIdle = false;
                break;
            }
        }

        if (allIdle)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)), null,
                new Rect(0, height - 1.5, width, 1.5));
            return;
        }

        for (var i = 0; i < Count; i++)
        {
            var value = Math.Clamp(_display[i], 0f, 1f);
            var barHeight = 3 + value * (height - 5);
            var x = i * (barWidth + gap);
            dc.DrawRoundedRectangle(BarBrush, null, new Rect(x, height - barHeight, barWidth, barHeight), radius, radius);
        }
    }
}