using System.Media;

namespace PawSip.Core;

/// <summary>Soft reminder sounds, synthesized in memory (no audio files to ship).</summary>
public static class Sounds
{
    private const int Rate = 22050;
    private static SoundPlayer? s_player;

    public static void Play(ReminderSound sound, int volume)
    {
        if (sound == ReminderSound.None || volume <= 0) return;
        try
        {
            var samples = sound switch
            {
                ReminderSound.Chime => Chime(),
                ReminderSound.Drop => Drop(),
                _ => Meow(),
            };
            var wav = ToWav(samples, MathEx.Clamp(volume, 0, 100) / 100.0);
            s_player?.Dispose();
            s_player = new SoundPlayer(new MemoryStream(wav));
            s_player.Play();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Sound");
        }
    }

    // Two gentle bell-like notes (C6 then G6).
    private static float[] Chime()
    {
        var buffer = new float[(int)(Rate * 1.3)];
        AddNote(buffer, 1046.5, 0.00, 0.55, 5.0);
        AddNote(buffer, 1568.0, 0.13, 0.45, 4.5);
        return buffer;
    }

    /// <summary>
    /// A small cat's "mi-a-ow": a voiced tone whose pitch rises then falls, shaped by moving vowel
    /// resonances (ee → ah → oo). Built additively, harmonic by harmonic, so it stays clean and soft.
    /// </summary>
    private static float[] Meow()
    {
        const double duration = 0.66;
        var count = (int)(Rate * duration);
        var buffer = new float[count];
        var phases = new double[40];
        var rng = new Random(7);

        for (var i = 0; i < count; i++)
        {
            var t = i / (double)Rate;
            var u = t / duration;

            // Pitch contour (Hz): up into the "a", then a long falling "ow", with a little vibrato.
            var f0 = u < 0.18 ? Lerp(560, 780, Smooth(u / 0.18))
                   : u < 0.42 ? Lerp(780, 840, Smooth((u - 0.18) / 0.24))
                   : Lerp(840, 470, Smooth((u - 0.42) / 0.58));
            f0 *= 1 + 0.012 * Math.Sin(2 * Math.PI * 6.2 * t);

            // Vowel resonances (a small animal, so higher than a human voice).
            var f1 = u < 0.25 ? Lerp(650, 1250, Smooth(u / 0.25)) : u < 0.65 ? 1250 : Lerp(1250, 800, Smooth((u - 0.65) / 0.35));
            var f2 = u < 0.25 ? Lerp(3000, 2100, Smooth(u / 0.25)) : u < 0.65 ? 2100 : Lerp(2100, 1400, Smooth((u - 0.65) / 0.35));
            const double f3 = 3900;

            // Soft attack, gentle fade at the end.
            var env = u < 0.08 ? Math.Pow(Math.Sin(Math.PI / 2 * u / 0.08), 2)
                    : u > 0.72 ? Math.Pow(Math.Cos(Math.PI / 2 * (u - 0.72) / 0.28), 2)
                    : 1.0;

            double sample = 0;
            for (var k = 1; k < phases.Length; k++)
            {
                var f = k * f0;
                if (f > 8000) break;
                phases[k] += 2 * Math.PI * f / Rate;
                var source = 1.0 / Math.Pow(k, 1.15);
                var shape = 0.12
                            + 1.0 / (1 + Math.Pow((f - f1) / 160, 2))
                            + 0.75 / (1 + Math.Pow((f - f2) / 240, 2))
                            + 0.30 / (1 + Math.Pow((f - f3) / 380, 2));
                sample += source * shape * Math.Sin(phases[k]);
            }
            sample += (rng.NextDouble() - 0.5) * 0.03; // a hint of breath
            buffer[i] = (float)(sample * env);
        }
        return buffer;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static double Smooth(double t) => t * t * (3 - 2 * t);

    // Two "bloop" water drops (fast rising pitch).
    private static float[] Drop()
    {
        var buffer = new float[(int)(Rate * 0.45)];
        AddBloop(buffer, 0.00, 380, 1300, 0.7);
        AddBloop(buffer, 0.17, 520, 1700, 0.45);
        return buffer;
    }

    private static void AddNote(float[] buffer, double freq, double start, double amp, double decay)
    {
        var offset = (int)(start * Rate);
        for (var i = 0; offset + i < buffer.Length; i++)
        {
            var t = i / (double)Rate;
            var env = Math.Min(1, t / 0.004) * Math.Exp(-t * decay);
            var v = Math.Sin(2 * Math.PI * freq * t)
                    + 0.25 * Math.Sin(2 * Math.PI * freq * 2.0 * t) * Math.Exp(-t * 4)
                    + 0.08 * Math.Sin(2 * Math.PI * freq * 3.01 * t) * Math.Exp(-t * 8);
            buffer[offset + i] += (float)(amp * env * v * 0.75);
        }
    }

    private static void AddBloop(float[] buffer, double start, double f0, double f1, double amp)
    {
        var offset = (int)(start * Rate);
        var length = (int)(0.16 * Rate);
        double phase = 0;
        for (var i = 0; i < length && offset + i < buffer.Length; i++)
        {
            var t = i / (double)Rate;
            var p = Math.Min(1, t / 0.07);
            var freq = f0 + (f1 - f0) * (1 - Math.Pow(1 - p, 2));
            phase += 2 * Math.PI * freq / Rate;
            var env = Math.Min(1, t / 0.003) * Math.Exp(-t * 28);
            buffer[offset + i] += (float)(amp * env * Math.Sin(phase));
        }
    }

    private static byte[] ToWav(float[] samples, double volume)
    {
        var peak = samples.Select(Math.Abs).DefaultIfEmpty(1).Max();
        var gain = peak > 0 ? 0.9 / peak * volume : 0;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var dataBytes = samples.Length * 2;
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);
        w.Write((short)1);       // PCM
        w.Write((short)1);       // mono
        w.Write(Rate);
        w.Write(Rate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8.ToArray());
        w.Write(dataBytes);
        foreach (var s in samples)
            w.Write((short)MathEx.Clamp(s * gain * short.MaxValue, short.MinValue, short.MaxValue));
        w.Flush();
        return ms.ToArray();
    }
}
