using System;
using System.IO;
using System.Media;

namespace Island.Services;

public static class NotificationSoundService
{
    public static void Play(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind == "none") return;

        try
        {
            if (kind == "system")
            {
                SystemSounds.Asterisk.Play();
                return;
            }

            var bright = kind == "bright";
            var stream = BuildChime(bright ? 1174.0 : 880.0, bright ? 1568.0 : 1318.5, bright);
            var player = new SoundPlayer(stream);
            player.Play();
        }
        catch
        {
        }
    }

    private static Stream BuildChime(double firstHz, double secondHz, bool bright)
    {
        const int sampleRate = 44100;
        const double duration = 0.85;
        var samples = (int)(sampleRate * duration);
        var data = new short[samples];
        var gapStart = (int)(sampleRate * 0.16);
        var amplitude = bright ? 0.32 : 0.24;

        for (var i = 0; i < samples; i++)
        {
            var t = i / (double)sampleRate;
            var envelope = Math.Min(1.0, t / 0.012) * Math.Exp(-3.2 * t);
            var value = 0.0;

            if (i < gapStart)
            {
                value = Math.Sin(2 * Math.PI * firstHz * t) * envelope;
            }
            else
            {
                var t2 = (i - gapStart) / (double)sampleRate;
                var env2 = Math.Min(1.0, t2 / 0.012) * Math.Exp(-3.0 * t2);
                value = Math.Sin(2 * Math.PI * secondHz * t2) * env2 * 0.85;
            }

            data[i] = (short)(value * amplitude * short.MaxValue);
        }

        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        var byteCount = data.Length * 2;

        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + byteCount);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(byteCount);
        foreach (var sample in data) writer.Write(sample);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }
}
