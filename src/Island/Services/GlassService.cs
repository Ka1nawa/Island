using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Island.Services;

public static class GlassService
{
    public static double MonitorWidth { get; private set; } = 1920;
    public static double MonitorHeight { get; private set; } = 1080;

    /// <summary>Physical screen origin of the monitor the glass is built for.</summary>
    public static double OriginX { get; private set; }
    public static double OriginY { get; private set; }

    /// <summary>Effective DPI scale of that monitor (1.0 = 96 DPI).</summary>
    public static double Scale { get; private set; } = 1.0;

    public static BitmapSource? Backdrop { get; private set; }

    public static void Initialize(DisplayMonitor? monitor = null)
    {
        var target = monitor ?? DisplayService.GetPrimary();
        Scale = target.Scale;
        OriginX = target.Left;
        OriginY = target.Top;
        MonitorWidth = target.Width / Scale;
        MonitorHeight = target.Height / Scale;
        Backdrop = Build();
    }

    private static BitmapSource Build()
    {
        try
        {
            var path = FindWallpaper();
            if (path is not null)
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.DecodePixelWidth = 480;
                image.EndInit();
                image.Freeze();

                var width = 480;
                var height = Math.Max(2, (int)Math.Round(width * MonitorHeight / Math.Max(1, MonitorWidth)));

                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    var scale = Math.Max(width / (double)image.PixelWidth, height / (double)image.PixelHeight);
                    var drawWidth = image.PixelWidth * scale;
                    var drawHeight = image.PixelHeight * scale;
                    dc.DrawImage(image, new Rect((width - drawWidth) / 2, (height - drawHeight) / 2, drawWidth, drawHeight));
                }

                var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rendered.Render(visual);
                return Blur(rendered, 4);
            }
        }
        catch
        {
            // fall through to the plain gradient
        }

        return Fallback();
    }

    private static string? FindWallpaper()
    {
        try
        {
            var transcoded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
            if (File.Exists(transcoded)) return transcoded;

            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var path = key?.GetValue("Wallpaper") as string;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
        }
        catch
        {
        }

        return null;
    }

    private static BitmapSource Fallback()
    {
        var width = 480;
        var height = 270;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var brush = new LinearGradientBrush(
                Color.FromRgb(24, 26, 44), Color.FromRgb(8, 8, 12), new Point(0, 0), new Point(1, 1));
            dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Two-pass box blur over a 32bpp Bgra buffer (shared with live glass).</summary>
    internal static void BlurBuffer(byte[] buffer, int width, int height, int radius)
    {
        var temp = new byte[buffer.Length];
        for (var pass = 0; pass < 2; pass++)
        {
            BoxHorizontal(buffer, temp, width, height, radius);
            BoxVertical(temp, buffer, width, height, radius);
        }
    }

    private static BitmapSource Blur(BitmapSource source, int radius)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = width * 4;
        var buffer = new byte[stride * height];
        source.CopyPixels(buffer, stride, 0);

        var temp = new byte[buffer.Length];
        for (var pass = 0; pass < 2; pass++)
        {
            BoxHorizontal(buffer, temp, width, height, radius);
            BoxVertical(temp, buffer, width, height, radius);
        }

        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, buffer, stride);
        result.Freeze();
        return result;
    }

    private static void BoxHorizontal(byte[] src, byte[] dst, int width, int height, int radius)
    {
        var window = radius * 2 + 1;
        for (var y = 0; y < height; y++)
        {
            var row = y * width * 4;
            int b = 0, g = 0, r = 0, a = 0;
            for (var i = -radius; i <= radius; i++)
            {
                var x = Math.Clamp(i, 0, width - 1) * 4 + row;
                b += src[x]; g += src[x + 1]; r += src[x + 2]; a += src[x + 3];
            }

            for (var x = 0; x < width; x++)
            {
                var index = row + x * 4;
                dst[index] = (byte)(b / window);
                dst[index + 1] = (byte)(g / window);
                dst[index + 2] = (byte)(r / window);
                dst[index + 3] = (byte)(a / window);

                var add = Math.Clamp(x + radius + 1, 0, width - 1) * 4 + row;
                var remove = Math.Clamp(x - radius, 0, width - 1) * 4 + row;
                b += src[add] - src[remove];
                g += src[add + 1] - src[remove + 1];
                r += src[add + 2] - src[remove + 2];
                a += src[add + 3] - src[remove + 3];
            }
        }
    }

    private static void BoxVertical(byte[] src, byte[] dst, int width, int height, int radius)
    {
        var window = radius * 2 + 1;
        for (var x = 0; x < width; x++)
        {
            var column = x * 4;
            int b = 0, g = 0, r = 0, a = 0;
            for (var i = -radius; i <= radius; i++)
            {
                var y = Math.Clamp(i, 0, height - 1) * width * 4 + column;
                b += src[y]; g += src[y + 1]; r += src[y + 2]; a += src[y + 3];
            }

            for (var y = 0; y < height; y++)
            {
                var index = y * width * 4 + column;
                dst[index] = (byte)(b / window);
                dst[index + 1] = (byte)(g / window);
                dst[index + 2] = (byte)(r / window);
                dst[index + 3] = (byte)(a / window);

                var add = Math.Clamp(y + radius + 1, 0, height - 1) * width * 4 + column;
                var remove = Math.Clamp(y - radius, 0, height - 1) * width * 4 + column;
                b += src[add] - src[remove];
                g += src[add + 1] - src[remove + 1];
                r += src[add + 2] - src[remove + 2];
                a += src[add + 3] - src[remove + 3];
            }
        }
    }
}
