using System;
using System.Windows;
using System.Windows.Media;

namespace Island.Controls;

/// <summary>
/// Small vector weather pictograms drawn in a 24x24 space.
/// </summary>
public sealed class WeatherIcon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(WeatherIcon),
        new FrameworkPropertyMetadata("cloudy", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(WeatherIcon),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size)) return;

        var offsetX = (ActualWidth - size) / 2;
        var offsetY = (ActualHeight - size) / 2;

        dc.PushTransform(new TranslateTransform(offsetX, offsetY));
        dc.PushTransform(new ScaleTransform(size / 24.0, size / 24.0));

        var fill = Fill;
        var pen = new Pen(fill, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        switch (Kind)
        {
            case "clear": DrawSun(dc, fill, pen, 12, 12, 4.4, 6.6, 9.4); break;
            case "clear-night": DrawMoon(dc, fill); break;
            case "partly": DrawPartly(dc, fill, pen, false); break;
            case "partly-night": DrawPartly(dc, fill, pen, true); break;
            case "cloudy": DrawCloud(dc, fill, 0, 0, 0.94); break;
            case "rain": DrawRain(dc, fill); break;
            case "snow": DrawSnow(dc, fill); break;
            case "storm": DrawStorm(dc, fill); break;
            case "fog": DrawFog(dc, pen); break;
            default: DrawCloud(dc, fill, 0, 0, 0.94); break;
        }

        dc.Pop();
        dc.Pop();
    }

    private static void DrawSun(DrawingContext dc, Brush fill, Pen pen,
        double cx, double cy, double radius, double rayStart, double rayEnd)
    {
        dc.DrawEllipse(fill, null, new Point(cx, cy), radius, radius);
        for (var i = 0; i < 8; i++)
        {
            var angle = Math.PI * 2 * i / 8;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            dc.DrawLine(pen,
                new Point(cx + cos * rayStart, cy + sin * rayStart),
                new Point(cx + cos * rayEnd, cy + sin * rayEnd));
        }
    }

    private static void DrawMoon(DrawingContext dc, Brush fill)
    {
        var outer = new EllipseGeometry(new Point(11.4, 12.4), 7.1, 7.1);
        var cut = new EllipseGeometry(new Point(15.6, 8.8), 6.3, 6.3);
        dc.DrawGeometry(fill, null, new CombinedGeometry(GeometryCombineMode.Exclude, outer, cut));
    }

    private static void DrawCloud(DrawingContext dc, Brush fill, double dx, double dy, double scale)
    {
        dc.PushTransform(new TranslateTransform(dx, dy));
        dc.PushTransform(new ScaleTransform(scale, scale));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(7.4, 18.4), true, true);
            ctx.BezierTo(new Point(5.0, 18.4), new Point(3.2, 16.5), new Point(3.2, 14.2), true, false);
            ctx.BezierTo(new Point(3.2, 12.0), new Point(4.9, 10.2), new Point(7.0, 10.1), true, false);
            ctx.BezierTo(new Point(7.8, 7.5), new Point(10.0, 5.8), new Point(12.6, 5.8), true, false);
            ctx.BezierTo(new Point(15.6, 5.8), new Point(18.0, 8.1), new Point(18.2, 10.9), true, false);
            ctx.BezierTo(new Point(20.5, 11.3), new Point(22.0, 12.9), new Point(22.0, 14.8), true, false);
            ctx.BezierTo(new Point(22.0, 16.9), new Point(20.4, 18.4), new Point(18.4, 18.4), true, false);
        }
        geometry.Freeze();

        dc.DrawGeometry(fill, null, geometry);
        dc.Pop();
        dc.Pop();
    }

    private static void DrawPartly(DrawingContext dc, Brush fill, Pen pen, bool night)
    {
        if (night)
        {
            dc.PushTransform(new ScaleTransform(0.62, 0.62));
            dc.PushTransform(new TranslateTransform(1.5, 0.5));
            DrawMoon(dc, fill);
            dc.Pop();
            dc.Pop();
        }
        else
        {
            DrawSun(dc, fill, pen, 8.3, 7.6, 2.9, 4.6, 6.4);
        }

        DrawCloud(dc, fill, 3.6, 5.2, 0.8);
    }

    private static void DrawRain(DrawingContext dc, Brush fill)
    {
        DrawCloud(dc, fill, 0.6, -1.8, 0.9);
        var pen = new Pen(fill, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(pen, new Point(8.6, 19.2), new Point(7.6, 22.0));
        dc.DrawLine(pen, new Point(13.0, 19.2), new Point(12.0, 22.0));
        dc.DrawLine(pen, new Point(17.4, 19.2), new Point(16.4, 22.0));
    }

    private static void DrawSnow(DrawingContext dc, Brush fill)
    {
        DrawCloud(dc, fill, 0.6, -2.2, 0.9);
        dc.DrawEllipse(fill, null, new Point(8.6, 20.4), 1.15, 1.15);
        dc.DrawEllipse(fill, null, new Point(12.6, 21.9), 1.15, 1.15);
        dc.DrawEllipse(fill, null, new Point(16.6, 20.4), 1.15, 1.15);
    }

    private static void DrawStorm(DrawingContext dc, Brush fill)
    {
        DrawCloud(dc, fill, 0.6, -2.4, 0.9);
        var bolt = new StreamGeometry();
        using (var ctx = bolt.Open())
        {
            ctx.BeginFigure(new Point(13.4, 15.6), true, true);
            ctx.LineTo(new Point(10.2, 20.6), true, false);
            ctx.LineTo(new Point(12.6, 20.6), true, false);
            ctx.LineTo(new Point(11.2, 23.4), true, false);
            ctx.LineTo(new Point(15.4, 18.4), true, false);
            ctx.LineTo(new Point(12.9, 18.4), true, false);
        }
        bolt.Freeze();
        dc.DrawGeometry(fill, null, bolt);
    }

    private static void DrawFog(DrawingContext dc, Pen pen)
    {
        pen.Thickness = 1.9;
        dc.DrawLine(pen, new Point(4.0, 9.0), new Point(20.0, 9.0));
        dc.DrawLine(pen, new Point(6.4, 13.2), new Point(19.2, 13.2));
        dc.DrawLine(pen, new Point(4.4, 17.4), new Point(17.6, 17.4));
        dc.DrawLine(pen, new Point(7.6, 21.2), new Point(15.0, 21.2));
    }
}
