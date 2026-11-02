using System;
using System.Runtime.InteropServices;
using NAudio.Dsp;
using NAudio.Wave;

namespace Island.Services;

public sealed class SpectrumService : IDisposable
{
    private const int FftSize = 1024;
    private const int BandCount = 28;

    private readonly Complex[] _buffer = new Complex[FftSize];
    private readonly float[] _magnitudes = new float[FftSize / 2];
    private readonly float[] _smooth = new float[BandCount];
    private readonly float[] _bands = new float[BandCount];
    private readonly object _gate = new();

    private WasapiLoopbackCapture? _capture;
    private int _fill;
    private int _sampleRate = 44100;
    private int _channels = 2;

    public bool Available { get; private set; }

    public void Start()
    {
        if (_capture is not null) return;

        try
        {
            _capture = new WasapiLoopbackCapture();
            _sampleRate = _capture.WaveFormat.SampleRate;
            _channels = Math.Max(1, _capture.WaveFormat.Channels);
            _capture.DataAvailable += OnData;
            _capture.StartRecording();
            Available = true;
        }
        catch
        {
            _capture?.Dispose();
            _capture = null;
            Available = false;
        }
    }

    public float[] Bands()
    {
        lock (_gate)
        {
            return (float[])_bands.Clone();
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var floats = MemoryMarshal.Cast<byte, float>(e.Buffer.AsSpan(0, e.BytesRecorded));
        for (var i = 0; i + _channels <= floats.Length; i += _channels)
        {
            var sample = 0f;
            for (var channel = 0; channel < _channels; channel++) sample += floats[i + channel];
            _buffer[_fill].X = sample / _channels;
            _buffer[_fill].Y = 0;
            _fill++;

            if (_fill < FftSize) continue;
            Analyze();
            _fill = 0;
        }
    }

    private void Analyze()
    {
        for (var i = 0; i < FftSize; i++)
        {
            _buffer[i].X *= 0.5f * (1f - (float)Math.Cos(2.0 * Math.PI * i / (FftSize - 1)));
        }

        FastFourierTransform.FFT(true, 10, _buffer);

        for (var i = 1; i < FftSize / 2; i++)
        {
            var magnitude = (float)Math.Sqrt(_buffer[i].X * _buffer[i].X + _buffer[i].Y * _buffer[i].Y);
            _magnitudes[i] = 20f * (float)Math.Log10(magnitude + 1e-7f);
        }

        var minHz = 45.0;
        var maxHz = Math.Min(16000.0, _sampleRate / 2.0);
        var next = new float[BandCount];

        for (var band = 0; band < BandCount; band++)
        {
            var f0 = minHz * Math.Pow(maxHz / minHz, band / (double)BandCount);
            var f1 = minHz * Math.Pow(maxHz / minHz, (band + 1.0) / BandCount);
            var i0 = Math.Max(1, (int)(f0 / _sampleRate * FftSize));
            var i1 = Math.Min(FftSize / 2 - 1, (int)(f1 / _sampleRate * FftSize));
            if (i1 <= i0) i1 = i0 + 1;

            var sum = 0f;
            for (var i = i0; i <= i1; i++) sum += _magnitudes[i];
            var db = sum / (i1 - i0 + 1);
            var level = Math.Clamp((db + 72f) / 60f, 0f, 1f);
            next[band] = (float)Math.Pow(level, 1.35);
        }

        lock (_gate)
        {
            for (var band = 0; band < BandCount; band++)
            {
                var target = next[band];
                var current = _smooth[band];
                _smooth[band] = current + (target - current) * (target > current ? 0.55f : 0.16f);
                _bands[band] = _smooth[band];
            }
        }
    }

    public void Dispose()
    {
        if (_capture is null) return;

        try
        {
            _capture.StopRecording();
            _capture.DataAvailable -= OnData;
            _capture.Dispose();
        }
        catch
        {
        }

        _capture = null;
        Available = false;
    }
}