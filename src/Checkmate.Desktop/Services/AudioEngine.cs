using System.Runtime.InteropServices;
using Avalonia.Threading;
using Checkmate.Core.Audio;

namespace Checkmate.Desktop.Services;

/// <summary>
/// A tiny direct binding to OpenAL (free, cross-platform audio): Windows uses the bundled OpenAL Soft,
/// macOS its built-in OpenAL framework, Linux the system libopenal. Only the handful of functions we need.
/// </summary>
internal static class OpenAl
{
    private const string Lib = "openal";
    private const CallingConvention Cdecl = CallingConvention.Cdecl;

    static OpenAl() => NativeLibrary.SetDllImportResolver(typeof(OpenAl).Assembly, Resolve);

    private static IntPtr Resolve(string name, System.Reflection.Assembly assembly, DllImportSearchPath? path)
    {
        if (name != Lib) return IntPtr.Zero;
        string[] candidates =
            OperatingSystem.IsWindows() ? new[] { "soft_oal.dll", "OpenAL32.dll", "openal.dll" } :
            OperatingSystem.IsMacOS()   ? new[] { "/System/Library/Frameworks/OpenAL.framework/OpenAL", "libopenal.1.dylib", "libopenal.dylib" } :
                                          new[] { "libopenal.so.1", "libopenal.so", "libopenal.so.0" };
        foreach (var candidate in candidates)
            if (NativeLibrary.TryLoad(candidate, assembly, path, out var handle)) return handle;
        return IntPtr.Zero;
    }

    // ---- devices and contexts ----
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern IntPtr alcOpenDevice([MarshalAs(UnmanagedType.LPStr)] string? name);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern byte alcCloseDevice(IntPtr device);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern IntPtr alcCreateContext(IntPtr device, IntPtr attributes);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern byte alcMakeContextCurrent(IntPtr context);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alcDestroyContext(IntPtr context);

    // ---- microphone ----
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern IntPtr alcCaptureOpenDevice([MarshalAs(UnmanagedType.LPStr)] string? name, uint frequency, int format, int bufferSize);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern byte alcCaptureCloseDevice(IntPtr device);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alcCaptureStart(IntPtr device);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alcCaptureStop(IntPtr device);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alcCaptureSamples(IntPtr device, short[] buffer, int samples);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alcGetIntegerv(IntPtr device, int param, int size, out int data);

    // ---- playback ----
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alGenBuffers(int n, out uint buffers);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alDeleteBuffers(int n, ref uint buffers);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alBufferData(uint buffer, int format, short[] data, int size, int frequency);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alGenSources(int n, out uint sources);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alDeleteSources(int n, ref uint sources);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alSourcei(uint source, int param, int value);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alSourcef(uint source, int param, float value);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alGetSourcei(uint source, int param, out int value);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alGetSourcef(uint source, int param, out float value);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alSourcePlay(uint source);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alSourceStop(uint source);
    [DllImport(Lib, CallingConvention = Cdecl)] public static extern void alSourceRewind(uint source);
}

/// <summary>
/// Plays the project's mixed sound and records from the microphone. Everything runs on the UI thread.
/// If sound is not available on this computer, <see cref="TryInit"/> says why and the rest of Checkmate keeps working.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    private const int AL_BUFFER = 0x1009, AL_SEC_OFFSET = 0x1024, AL_SOURCE_STATE = 0x1010, AL_PLAYING = 0x1012;
    private const int AL_FORMAT_MONO16 = 0x1101, AL_FORMAT_STEREO16 = 0x1103;
    private const int ALC_CAPTURE_SAMPLES = 0x312;
    private const int CaptureRate = 44100;

    private IntPtr _device, _context, _capture;
    private uint _source, _buffer;
    private bool _initialized;
    private string? _initError;
    private double _mixSeconds;
    private readonly List<short> _recorded = new();
    private readonly DispatcherTimer _snippetTimer = new();

    public AudioEngine()
    {
        _snippetTimer.Tick += (_, _) => { _snippetTimer.Stop(); Stop(); };
    }

    public bool IsRecording => _capture != IntPtr.Zero;
    public double RecordedSeconds => _recorded.Count / (double)CaptureRate;

    public bool TryInit(out string? error)
    {
        if (_initialized) { error = null; return true; }
        if (_initError != null) { error = _initError; return false; }
        try
        {
            _device = OpenAl.alcOpenDevice(null);
            if (_device == IntPtr.Zero) throw new InvalidOperationException("No sound output device was found.");
            _context = OpenAl.alcCreateContext(_device, IntPtr.Zero);
            if (_context == IntPtr.Zero || OpenAl.alcMakeContextCurrent(_context) == 0)
                throw new InvalidOperationException("Could not start the sound system.");
            OpenAl.alGenSources(1, out _source);
            _initialized = true;
            error = null;
            return true;
        }
        catch (DllNotFoundException)
        {
            _initError = "The OpenAL sound library was not found. On Linux install it with: sudo apt install libopenal1";
        }
        catch (Exception ex)
        {
            _initError = ex.Message;
        }
        error = _initError;
        return false;
    }

    // ------------------------------------------------------------ playback

    /// <summary>Hand the engine the finished stereo mix (project time 0 = first sample).</summary>
    public void SetMix(short[] stereo, int sampleRate)
    {
        if (!_initialized) return;
        OpenAl.alSourceStop(_source);
        OpenAl.alSourcei(_source, AL_BUFFER, 0);
        if (_buffer != 0) { OpenAl.alDeleteBuffers(1, ref _buffer); _buffer = 0; }

        _mixSeconds = stereo.Length / 2.0 / sampleRate;
        if (stereo.Length < 4) return;
        OpenAl.alGenBuffers(1, out _buffer);
        OpenAl.alBufferData(_buffer, AL_FORMAT_STEREO16, stereo, stereo.Length * sizeof(short), sampleRate);
        OpenAl.alSourcei(_source, AL_BUFFER, (int)_buffer);
    }

    public bool Play(double fromSeconds)
    {
        if (!_initialized || _buffer == 0 || fromSeconds >= _mixSeconds) return false;
        _snippetTimer.Stop();
        OpenAl.alSourceRewind(_source);
        OpenAl.alSourcef(_source, AL_SEC_OFFSET, (float)Math.Max(0, fromSeconds));
        OpenAl.alSourcePlay(_source);
        return true;
    }

    /// <summary>A short burst of sound, used when you step through frames (to find the right mouth shape).</summary>
    public void PlaySnippet(double fromSeconds, double durationSeconds)
    {
        if (!Play(fromSeconds)) return;
        _snippetTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.03, durationSeconds));
        _snippetTimer.Start();
    }

    public void Stop()
    {
        if (!_initialized) return;
        _snippetTimer.Stop();
        OpenAl.alSourceStop(_source);
    }

    public bool IsPlaying
    {
        get
        {
            if (!_initialized) return false;
            OpenAl.alGetSourcei(_source, AL_SOURCE_STATE, out int state);
            return state == AL_PLAYING;
        }
    }

    /// <summary>Seconds since the start of the mix. The animation follows this clock so picture and sound never drift apart.</summary>
    public double PositionSeconds
    {
        get
        {
            if (!_initialized) return 0;
            OpenAl.alGetSourcef(_source, AL_SEC_OFFSET, out float seconds);
            return seconds;
        }
    }

    // ------------------------------------------------------------ recording

    public bool StartRecording(out string? error)
    {
        error = null;
        if (IsRecording) return true;
        try
        {
            _capture = OpenAl.alcCaptureOpenDevice(null, CaptureRate, AL_FORMAT_MONO16, CaptureRate * 2);
            if (_capture == IntPtr.Zero)
            {
                error = "No microphone was found, or access to it is blocked. " +
                        "(Windows: Settings > Privacy > Microphone. macOS: System Settings > Privacy & Security > Microphone.)";
                return false;
            }
            _recorded.Clear();
            OpenAl.alcCaptureStart(_capture);
            return true;
        }
        catch (Exception ex)
        {
            _capture = IntPtr.Zero;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Move whatever the microphone has captured so far into our buffer. Call every few dozen milliseconds.</summary>
    public void PollRecording()
    {
        if (!IsRecording) return;
        OpenAl.alcGetIntegerv(_capture, ALC_CAPTURE_SAMPLES, 1, out int available);
        if (available <= 0) return;
        var chunk = new short[available];
        OpenAl.alcCaptureSamples(_capture, chunk, available);
        _recorded.AddRange(chunk);
    }

    public AudioData? StopRecording()
    {
        if (!IsRecording) return null;
        PollRecording();
        OpenAl.alcCaptureStop(_capture);
        OpenAl.alcCaptureCloseDevice(_capture);
        _capture = IntPtr.Zero;

        var data = _recorded.Count > 0 ? new AudioData(CaptureRate, 1, _recorded.ToArray()) : null;
        _recorded.Clear();
        return data;
    }

    public void Dispose()
    {
        try
        {
            StopRecording();
            if (!_initialized) return;
            OpenAl.alSourceStop(_source);
            OpenAl.alSourcei(_source, AL_BUFFER, 0);
            if (_buffer != 0) OpenAl.alDeleteBuffers(1, ref _buffer);
            OpenAl.alDeleteSources(1, ref _source);
            OpenAl.alcMakeContextCurrent(IntPtr.Zero);
            OpenAl.alcDestroyContext(_context);
            OpenAl.alcCloseDevice(_device);
        }
        catch { /* shutting down anyway */ }
        _initialized = false;
    }
}
