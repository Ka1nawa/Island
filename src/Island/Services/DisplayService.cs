using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace Island.Services;

/// <summary>Monitor geometry in physical pixels, with its effective DPI.</summary>
public sealed class DisplayMonitor
{
    public string DeviceName { get; init; } = "";
    public int Left { get; init; }
    public int Top { get; init; }
    public int Right { get; init; }
    public int Bottom { get; init; }
    public int WorkLeft { get; init; }
    public int WorkTop { get; init; }
    public int WorkRight { get; init; }
    public int WorkBottom { get; init; }
    public uint DpiX { get; init; } = 96;
    public uint DpiY { get; init; } = 96;
    public bool IsPrimary { get; init; }

    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public double Scale => Math.Max(1.0, DpiX) / 96.0;

    public string DisplayName
    {
        get
        {
            var name = DeviceName;
            var slash = name.LastIndexOf('\\');
            if (slash >= 0) name = name[(slash + 1)..];
            return name + " · " + Width + "×" + Height;
        }
    }
}

/// <summary>
/// Monitor enumeration, effective DPI and the usable work area.
/// The work area subtracts the taskbar strip even when the taskbar is auto-hidden
/// (in that state SystemParameters.WorkArea reports the full screen).
/// </summary>
public static class DisplayService
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(uint message, ref APPBARDATA data);

    private const uint MonitorInfoPrimary = 1;
    private const int MdtEffectiveDpi = 0;
    private const uint AbmGetTaskbarPos = 5;

    public static IReadOnlyList<DisplayMonitor> GetMonitors()
    {
        var monitors = new List<DisplayMonitor>();

        bool Callback(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data)
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                uint dpiX = 96, dpiY = 96;
                try
                {
                    if (GetDpiForMonitor(hMonitor, MdtEffectiveDpi, out var x, out var y) == 0)
                    {
                        dpiX = x;
                        dpiY = y;
                    }
                }
                catch
                {
                    // Pre-8.1 or restricted host: keep 96 DPI.
                }

                monitors.Add(new DisplayMonitor
                {
                    DeviceName = info.szDevice ?? "",
                    Left = info.rcMonitor.Left,
                    Top = info.rcMonitor.Top,
                    Right = info.rcMonitor.Right,
                    Bottom = info.rcMonitor.Bottom,
                    WorkLeft = info.rcWork.Left,
                    WorkTop = info.rcWork.Top,
                    WorkRight = info.rcWork.Right,
                    WorkBottom = info.rcWork.Bottom,
                    DpiX = dpiX,
                    DpiY = dpiY,
                    IsPrimary = (info.dwFlags & MonitorInfoPrimary) != 0
                });
            }

            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        return monitors;
    }

    public static DisplayMonitor GetPrimary()
    {
        var monitors = GetMonitors();
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault() ?? Fallback();
    }

    public static DisplayMonitor ResolveMonitor(string? id, IntPtr window)
    {
        var monitors = GetMonitors();
        if (monitors.Count == 0) return Fallback();

        if (!string.IsNullOrWhiteSpace(id) && !IsMode(id, "primary") && !IsMode(id, "cursor"))
        {
            var named = monitors.FirstOrDefault(m =>
                string.Equals(m.DeviceName, id, StringComparison.OrdinalIgnoreCase));
            if (named is not null) return named;
        }

        if (IsMode(id, "cursor") && GetCursorPos(out var point))
        {
            var under = monitors.FirstOrDefault(m =>
                point.X >= m.Left && point.X < m.Right && point.Y >= m.Top && point.Y < m.Bottom);
            if (under is not null) return under;
        }

        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    /// <summary>Work area of the monitor with the taskbar strip removed, in physical pixels.</summary>
    public static RECT GetUsableWorkArea(DisplayMonitor monitor)
    {
        var work = new RECT
        {
            Left = monitor.WorkLeft,
            Top = monitor.WorkTop,
            Right = monitor.WorkRight,
            Bottom = monitor.WorkBottom
        };

        if (!TryGetTaskbar(out var bar, out var edge)) return work;

        var overlaps = bar.Right > monitor.Left && bar.Left < monitor.Right &&
                       bar.Bottom > monitor.Top && bar.Top < monitor.Bottom;
        if (!overlaps) return work;

        switch (edge)
        {
            case 3: // ABE_BOTTOM
                if (work.Bottom > bar.Top) work.Bottom = Math.Max(work.Top, bar.Top);
                break;
            case 1: // ABE_TOP
                if (work.Top < bar.Bottom) work.Top = Math.Min(work.Bottom, bar.Bottom);
                break;
            case 0: // ABE_LEFT
                if (work.Left < bar.Right) work.Left = Math.Min(work.Right, bar.Right);
                break;
            case 2: // ABE_RIGHT
                if (work.Right > bar.Left) work.Right = Math.Max(work.Left, bar.Left);
                break;
        }

        return work;
    }

    public static bool TryGetTaskbar(out RECT rect, out uint edge)
    {
        var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
        if (SHAppBarMessage(AbmGetTaskbarPos, ref data) != IntPtr.Zero && data.rc.Width > 0 && data.rc.Height > 0)
        {
            rect = data.rc;
            edge = data.uEdge;
            return true;
        }

        rect = default;
        edge = 3;
        return false;
    }

    private static bool IsMode(string? id, string mode) =>
        string.Equals(id, mode, StringComparison.OrdinalIgnoreCase);

    private static DisplayMonitor Fallback()
    {
        var width = (int)Math.Round(SystemParameters.PrimaryScreenWidth);
        var height = (int)Math.Round(SystemParameters.PrimaryScreenHeight);
        return new DisplayMonitor
        {
            DeviceName = "PRIMARY",
            Right = width,
            Bottom = height,
            WorkRight = width,
            WorkBottom = height,
            IsPrimary = true
        };
    }
}
