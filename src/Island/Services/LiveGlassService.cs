using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace Island.Services;

/// <summary>
/// Captures the real desktop content behind the island.
/// The window is excluded from capture with WDA_EXCLUDEFROMCAPTURE, so the
/// island never captures itself; protected (DRM) content comes back black.
/// </summary>
public static class LiveGlassService
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

    public const uint WdaNone = 0x00000000;
    public const uint WdaExcludeFromCapture = 0x00000011;

    public static string LastError { get; private set; } = "";

    public static bool SetExcluded(IntPtr hwnd, bool excluded)
    {
        if (hwnd == IntPtr.Zero) return false;
        try
        {
            return SetWindowDisplayAffinity(hwnd, excluded ? WdaExcludeFromCapture : WdaNone);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Grabs a physical screen rectangle, downsamples it and blurs it.
    /// Returns null when the capture is unavailable (secure desktop, remote session, etc.).
    /// </summary>
    public static BitmapSource? Grab(int x, int y, int width, int height)
    {
        if (width < 8 || height < 8) return null;
        width = Math.Min(width, 4096);
        height = Math.Min(height, 4096);

        try
        {
            using var full = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(full))
            {
                graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            }

            var smallWidth = Math.Max(4, width / 4);
            var smallHeight = Math.Max(4, height / 4);

            using var small = new Bitmap(smallWidth, smallHeight, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(small))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(full,
                    new Rectangle(0, 0, smallWidth, smallHeight),
                    new Rectangle(0, 0, width, height),
                    GraphicsUnit.Pixel);
            }

            var rect = new Rectangle(0, 0, smallWidth, smallHeight);
            var data = small.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var stride = data.Stride;
                var buffer = new byte[stride * smallHeight];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                GlassService.BlurBuffer(buffer, smallWidth, smallHeight, 9);

                var image = BitmapSource.Create(smallWidth, smallHeight, 96, 96,
                    System.Windows.Media.PixelFormats.Bgra32, null, buffer, stride);
                image.Freeze();
                return image;
            }
            finally
            {
                small.UnlockBits(data);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.GetType().Name + ": " + ex.Message;
            return null;
        }
    }
}
