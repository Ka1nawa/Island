using System;
using Island.Core;
using Island.Services;

namespace Island.ViewModels;

public sealed class SystemViewModel : ObservableObject
{
    private readonly HardwareMonitor _monitor;

    public SystemViewModel(HardwareMonitor monitor) => _monitor = monitor;

    private double _cpu;
    public double CpuValue => _cpu;
    public string CpuText => Math.Round(_cpu).ToString("0") + "%";

    private double _gpu;
    private bool _gpuAvailable;
    public double GpuValue => _gpu;
    public string GpuText => _gpuAvailable ? Math.Round(_gpu).ToString("0") + "%" : L.T("NotAvailable");

    private double _ram;
    private double _ramUsed;
    private double _ramTotal;
    public double RamValue => _ram;
    public string RamText => Math.Round(_ram).ToString("0") + "%" +
                             ( _ramTotal > 0 ? $" · {_ramUsed:0.0} / {_ramTotal:0.0} GB" : "");

    public void Update()
    {
        var snapshot = _monitor.Sample();
        _cpu = snapshot.Cpu;
        _gpu = snapshot.Gpu;
        _gpuAvailable = snapshot.GpuAvailable;
        _ram = snapshot.Ram;
        _ramUsed = snapshot.RamUsedGb;
        _ramTotal = snapshot.RamTotalGb;

        OnPropertyChanged(string.Empty);
    }
}
