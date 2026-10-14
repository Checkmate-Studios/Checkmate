using System.Text;
using Checkmate.Core.Model;

namespace Checkmate.Core.Audio;

/// <summary>Decoded sound: interleaved 16-bit samples.</summary>
public sealed class AudioData
{
    public AudioData(int sampleRate, int channels, short[] samples)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        SampleRate = sampleRate;
        Channels = channels;
        Samples = samples;
    }

    public int SampleRate { get; }
    public int Channels { get; }
    public short[] Samples { get; }

    /// <summary>Number of sample frames (one sample per channel). Not to be confused with animation frames.</summary>
    public int SampleFrames => Samples.Length / Channels;
    public double Duration => (double)SampleFrames / SampleRate;

    /// <summary>Loudest sample per bucket, 0..1. Used to draw the waveform.</summary>
    public float[] GetPeaks(int buckets)
    {
        buckets = Math.Max(1, buckets);
        var peaks = new float[buckets];
        int n = SampleFrames;
        if (n == 0) return peaks;

        for (int b = 0; b < buckets; b++)
        {
            int from = (int)((long)b * n / buckets);
            int to = Math.Min(n, Math.Max(from + 1, (int)((long)(b + 1) * n / buckets)));
            int max = 0;
            for (int i = from; i < to; i++)
                for (int c = 0; c < Channels; c++)
                {
                    int v = Math.Abs((int)Samples[i * Channels + c]);
                    if (v > max) max = v;
                }
            peaks[b] = Math.Min(1f, max / 32768f);
        }
        return peaks;
    }
}

/// <summary>Reads and writes WAV files (no extra libraries). Reads 8/16/24/32-bit PCM and 32-bit float.</summary>
public static class WavFile
{
    public static void Write(string path, AudioData data)
    {
        using var fs = File.Create(path);
        Write(fs, data);
    }

    public static void Write(Stream stream, AudioData data)
    {
        using var w = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        int dataBytes = data.Samples.Length * 2;
        w.Write(new[] { 'R', 'I', 'F', 'F' });
        w.Write(36 + dataBytes);
        w.Write(new[] { 'W', 'A', 'V', 'E' });
        w.Write(new[] { 'f', 'm', 't', ' ' });
        w.Write(16);
        w.Write((short)1);                                   // PCM
        w.Write((short)data.Channels);
        w.Write(data.SampleRate);
        w.Write(data.SampleRate * data.Channels * 2);
        w.Write((short)(data.Channels * 2));
        w.Write((short)16);
        w.Write(new[] { 'd', 'a', 't', 'a' });
        w.Write(dataBytes);
        var bytes = new byte[dataBytes];
        Buffer.BlockCopy(data.Samples, 0, bytes, 0, dataBytes);   // little-endian on every platform we run on
        w.Write(bytes);
    }

    public static AudioData Read(string path)
    {
        using var fs = File.OpenRead(path);
        return Read(fs);
    }

    public static AudioData Read(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (new string(r.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not a WAV file.");
        r.ReadInt32();
        if (new string(r.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not a WAV file.");

        int tag = 0, channels = 0, rate = 0, bits = 0;
        byte[]? data = null;

        while (stream.Position + 8 <= stream.Length)
        {
            string id = new string(r.ReadChars(4));
            int size = r.ReadInt32();
            long next = stream.Position + size + (size & 1);

            if (id == "fmt ")
            {
                tag = r.ReadUInt16();
                channels = r.ReadUInt16();
                rate = r.ReadInt32();
                r.ReadInt32();
                r.ReadUInt16();
                bits = r.ReadUInt16();
                if (tag == 0xFFFE && size >= 40)             // "extensible": the real format is in the sub-format
                {
                    r.ReadUInt16(); r.ReadUInt16(); r.ReadInt32();
                    tag = r.ReadUInt16();
                }
            }
            else if (id == "data")
            {
                long available = stream.Length - stream.Position;
                long length = size <= 0 || size > available ? available : size;
                data = r.ReadBytes((int)length);
                break;
            }

            if (next > stream.Length || next <= stream.Position - 8) break;
            stream.Position = next;
        }

        if (data == null || channels <= 0 || rate <= 0 || bits <= 0)
            throw new InvalidDataException("This WAV file is incomplete.");

        int bytesPer = bits / 8;
        int count = data.Length / bytesPer;
        count -= count % channels;
        var samples = new short[count];

        switch (tag, bits)
        {
            case (1, 8):
                for (int i = 0; i < count; i++) samples[i] = (short)((data[i] - 128) << 8);
                break;
            case (1, 16):
                Buffer.BlockCopy(data, 0, samples, 0, count * 2);
                break;
            case (1, 24):
                for (int i = 0; i < count; i++)
                {
                    int v = data[i * 3] | (data[i * 3 + 1] << 8) | ((sbyte)data[i * 3 + 2] << 16);
                    samples[i] = (short)(v >> 8);
                }
                break;
            case (1, 32):
                for (int i = 0; i < count; i++) samples[i] = (short)(BitConverter.ToInt32(data, i * 4) >> 16);
                break;
            case (3, 32):
                for (int i = 0; i < count; i++)
                    samples[i] = (short)Math.Clamp(BitConverter.ToSingle(data, i * 4) * 32767f, -32768f, 32767f);
                break;
            default:
                throw new NotSupportedException($"Unsupported WAV format (type {tag}, {bits}-bit).");
        }

        return new AudioData(rate, channels, samples);
    }
}

/// <summary>Mixes all sound clips of a project into one stereo 16-bit stream.</summary>
public static class AudioMixer
{
    public const int SampleRate = 44100;

    /// <summary>Stereo (L,R interleaved) mix of the time span [fromSeconds, toSeconds). Clips sit at StartFrame / Fps seconds.</summary>
    public static short[] Mix(Project project, double fromSeconds, double toSeconds, int sampleRate = SampleRate)
    {
        int frames = Math.Max(0, (int)Math.Round((toSeconds - fromSeconds) * sampleRate));
        var acc = new float[frames * 2];
        int fps = Math.Max(1, project.Fps);

        foreach (var clip in project.Audio)
        {
            var d = clip.Data;
            if (d == null || clip.Muted || d.SampleFrames == 0) continue;

            double clipStart = clip.StartFrame / (double)fps;
            double gain = clip.Volume;                       // samples stay on the 16-bit scale
            int first = Math.Max(0, (int)Math.Ceiling((clipStart - fromSeconds) * sampleRate));
            int last = Math.Min(frames, (int)Math.Floor((clipStart + d.Duration - fromSeconds) * sampleRate));
            int ch = d.Channels, srcFrames = d.SampleFrames;
            var s = d.Samples;

            for (int i = first; i < last; i++)
            {
                double pos = (fromSeconds + i / (double)sampleRate - clipStart) * d.SampleRate;
                int i0 = (int)pos;
                if (i0 < 0) i0 = 0;
                if (i0 >= srcFrames) continue;
                int i1 = Math.Min(i0 + 1, srcFrames - 1);
                float f = (float)(pos - i0);                      // linear interpolation handles different sample rates

                float l0 = s[i0 * ch], l1 = s[i1 * ch];
                float r0 = ch > 1 ? s[i0 * ch + 1] : l0, r1 = ch > 1 ? s[i1 * ch + 1] : l1;
                acc[i * 2] += (float)((l0 + (l1 - l0) * f) * gain);
                acc[i * 2 + 1] += (float)((r0 + (r1 - r0) * f) * gain);
            }
        }

        var output = new short[acc.Length];
        for (int i = 0; i < acc.Length; i++)
            output[i] = (short)Math.Clamp(acc[i], -32768f, 32767f);
        return output;
    }
}
