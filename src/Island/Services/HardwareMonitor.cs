using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Island.Services;

public sealed class HardwareSnapshot
{
    public double Cpu { get; init; }
    public double Gpu { get; init; }
    public bool GpuAvailable { get; init; }
    public double Ram { get; init; }
    public double RamUsedGb { get; init; }
    public double RamTotalGb { get; init; }
}

/// <summary>CPU (GetSystemTimes), RAM (GlobalMemoryStatusEx) and GPU (GPU Engine counters).</summary>
public sealed class HardwareMonitor : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX status);

    private long _prevIdle;
    private long _prevKernel;
    private long _prevUser;
    private bool _hasCpuSample;
    private double _lastCpu;

    private PerformanceCounterCategory? _gpuCategory;
    private List<PerformanceCounter> _gpuCounters = new();
    private DateTime _gpuRefresh = DateTime.MinValue;
    private double _lastGpu;
    private bool _gpuAvailable;

    public HardwareMonitor()
    {
        try
        {
            _gpuCategory = new PerformanceCounterCategory("GPU Engine");
            _gpuAvailable = true;
        }
        catch
        {
            _gpuAvailable = false;
        }

        RefreshGpuCounters();
        SampleCpu();
    }

    public HardwareSnapshot Sample()
    {
        var cpu = SampleCpu();
        var gpu = SampleGpu();

        var ram = 0.0;
        var used = 0.0;
        var total = 0.0;

        var status = new MEMORYSTATUSEX { Length = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref status))
        {
            ram = status.MemoryLoad;
            total = status.TotalPhys / 1024.0 / 1024.0 / 1024.0;
            used = (status.TotalPhys - status.AvailPhys) / 1024.0 / 1024.0 / 1024.0;
        }

        return new HardwareSnapshot
        {
            Cpu = cpu,
            Gpu = gpu,
            GpuAvailable = _gpuAvailable,
            Ram = ram,
            RamUsedGb = used,
            RamTotalGb = total
        };
    }

    private double SampleCpu()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
            return _lastCpu;

        var idle = ToLong(idleTime);
        var kernel = ToLong(kernelTime);
        var user = ToLong(userTime);

        if (!_hasCpuSample)
        {
            _prevIdle = idle;
            _prevKernel = kernel;
            _prevUser = user;
            _hasCpuSample = true;
            return 0;
        }

        var idleDelta = idle - _prevIdle;
        var totalDelta = (kernel - _prevKernel) + (user - _prevUser);

        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;

        if (totalDelta <= 0) return _lastCpu;

        _lastCpu = Math.Clamp(100.0 * (1.0 - idleDelta / (double)totalDelta), 0, 100);
        return _lastCpu;
    }

    private double SampleGpu()
    {
        if (!_gpuAvailable || _gpuCategory is null) return 0;

        if (DateTime.UtcNow - _gpuRefresh > TimeSpan.FromSeconds(20))
            RefreshGpuCounters();

        try
        {
            var sum = 0.0;
            var any = false;

            foreach (var counter in _gpuCounters)
            {
                try
                {
                    var value = counter.NextValue();
                    if (value <= 0) continue;
                    sum += value;
                    any = true;
                }
                catch
                {
                }
            }

            if (!any) return _lastGpu;
            _lastGpu = Math.Clamp(sum, 0, 100);
            return _lastGpu;
        }
        catch
        {
            return _lastGpu;
        }
    }

    private void RefreshGpuCounters()
    {
        _gpuRefresh = DateTime.UtcNow;

        try
        {
            if (_gpuCategory is null) return;

            foreach (var counter in _gpuCounters) counter.Dispose();
            _gpuCounters = new List<PerformanceCounter>();

            var instances = _gpuCategory.GetInstanceNames()
                .Where(name => name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (instances.Length == 0) instances = _gpuCategory.GetInstanceNames();

            foreach (var instance in instances)
            {
                try
                {
                    var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true);
                    counter.NextValue();
                    _gpuCounters.Add(counter);
                }
                catch
                {
                }
            }

            _gpuAvailable = _gpuCounters.Count > 0;
        }
        catch
        {
            _gpuAvailable = false;
        }
    }

    private static long ToLong(FILETIME time) => ((long)time.High << 32) | time.Low;

    public void Dispose()
    {
        foreach (var counter in _gpuCounters) counter.Dispose();
        _gpuCounters.Clear();
    }
}
