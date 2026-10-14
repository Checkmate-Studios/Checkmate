using Checkmate.Core.Audio;
using Checkmate.Core.Editing;
using Checkmate.Core.IO;
using Checkmate.Core.Model;
using Xunit;

public class AudioTests
{
    private static AudioData Constant(int rate, int channels, double seconds, short level)
    {
        var frames = (int)(rate * seconds);
        var s = new short[frames * channels];
        Array.Fill(s, level);
        return new AudioData(rate, channels, s);
    }

    private static Project ProjectWith(int fps, params AudioClip[] clips)
    {
        var p = new Project { Fps = fps };
        p.Audio.AddRange(clips);
        return p;
    }

    [Fact]
    public void Wav_roundtrip_keeps_everything()
    {
        var samples = new short[2000];
        for (int i = 0; i < samples.Length; i++) samples[i] = (short)(i * 7 - 5000);
        var original = new AudioData(22050, 2, samples);

        var stream = new MemoryStream();
        WavFile.Write(stream, original);
        stream.Position = 0;
        var loaded = WavFile.Read(stream);

        Assert.Equal(22050, loaded.SampleRate);
        Assert.Equal(2, loaded.Channels);
        Assert.Equal(samples, loaded.Samples);
    }

    [Fact]
    public void Peaks_show_the_loudest_sample_in_each_bucket()
    {
        var s = new short[1000];
        s[950] = 16384;                                  // a spike in the last tenth
        var peaks = new AudioData(1000, 1, s).GetPeaks(10);

        Assert.Equal(10, peaks.Length);
        Assert.Equal(0f, peaks[0]);
        Assert.InRange(peaks[9], 0.499f, 0.501f);
    }

    [Fact]
    public void Mixer_puts_a_clip_at_its_start_frame()
    {
        var clip = new AudioClip { Data = Constant(44100, 1, 1.0, 16384), StartFrame = 5 };   // starts at 0.5 s (10 fps)
        var mix = AudioMixer.Mix(ProjectWith(10, clip), 0, 2);

        Assert.Equal(88200 * 2, mix.Length);
        Assert.Equal(0, mix[(int)(0.25 * 44100) * 2]);                      // before the clip
        Assert.InRange((int)mix[(int)(0.75 * 44100) * 2], 16000, 16384);    // during the clip
        Assert.Equal(0, mix[(int)(1.75 * 44100) * 2]);                      // after the clip
    }

    [Fact]
    public void Mixer_slices_a_time_range()
    {
        var clip = new AudioClip { Data = Constant(44100, 1, 1.0, 10000), StartFrame = 10 };  // 1.0 s .. 2.0 s at 10 fps
        var mix = AudioMixer.Mix(ProjectWith(10, clip), 1.5, 2.5);

        Assert.Equal(44100 * 2, mix.Length);
        Assert.InRange((int)mix[0], 9000, 10000);                           // 1.5 s is inside the clip
        Assert.Equal(0, mix[(int)(0.75 * 44100) * 2]);                      // 2.25 s is after it
    }

    [Fact]
    public void Mixer_applies_volume_sums_clips_and_clamps()
    {
        var a = new AudioClip { Data = Constant(44100, 1, 1, 20000), Volume = 0.5 };
        var b = new AudioClip { Data = Constant(44100, 1, 1, 20000) };
        var c = new AudioClip { Data = Constant(44100, 1, 1, 20000) };

        var half = AudioMixer.Mix(ProjectWith(10, a), 0, 0.5);
        Assert.InRange((int)half[1000], 9900, 10100);

        var loud = AudioMixer.Mix(ProjectWith(10, b, c), 0, 0.5);           // 20000 + 20000 would overflow
        Assert.Equal(short.MaxValue, loud[1000]);
    }

    [Fact]
    public void Mixer_ignores_muted_and_missing_clips()
    {
        var muted = new AudioClip { Data = Constant(44100, 1, 1, 20000), Muted = true };
        var missing = new AudioClip { Data = null, Missing = true };
        var mix = AudioMixer.Mix(ProjectWith(10, muted, missing), 0, 0.5);
        Assert.All(mix, v => Assert.Equal(0, v));
    }

    [Fact]
    public void Mixer_handles_other_sample_rates_and_stereo()
    {
        var slow = new AudioClip { Data = Constant(22050, 2, 1.0, 8000) };
        var mix = AudioMixer.Mix(ProjectWith(10, slow), 0, 1);
        Assert.InRange((int)mix[44100], 7900, 8000);                        // still audible halfway through
        Assert.InRange((int)mix[44100 + 1], 7900, 8000);                    // both channels
    }

    [Fact]
    public void Editor_sound_changes_are_undoable_and_bump_the_revision()
    {
        var ed = new Editor();
        var clip = new AudioClip { Name = "song", Data = Constant(8000, 1, 1, 100) };
        int rev = ed.AudioRevision;

        ed.AddAudioClip(clip);
        Assert.Single(ed.Project.Audio);
        Assert.True(ed.AudioRevision > rev);

        ed.SetClipStart(clip, 12);
        Assert.Equal(12, clip.StartFrame);
        ed.Undo();
        Assert.Equal(0, clip.StartFrame);

        ed.Undo();
        Assert.Empty(ed.Project.Audio);
        ed.Redo();
        Assert.Single(ed.Project.Audio);

        ed.RemoveAudioClip(clip);
        Assert.Empty(ed.Project.Audio);
        ed.Undo();
        Assert.Single(ed.Project.Audio);
    }

    [Fact]
    public void Extend_animation_only_grows()
    {
        var ed = new Editor();
        ed.EnsureFrameCount(100);
        Assert.Equal(100, ed.Project.FrameCount);
        ed.EnsureFrameCount(10);
        Assert.Equal(100, ed.Project.FrameCount);
    }

    [Fact]
    public void Sound_is_saved_next_to_the_project_and_loads_back()
    {
        string dir = Path.Combine(Path.GetTempPath(), "checkmate_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var ed = new Editor();
            var data = Constant(8000, 1, 0.5, 1234);
            ed.AddAudioClip(new AudioClip { Name = "My Song!", Data = data, StartFrame = 3, Volume = 0.8 });

            string path = Path.Combine(dir, "film.checkmate");
            ProjectIO.Save(ed.Project, path);

            string folder = Path.Combine(dir, "film_audio");
            Assert.True(Directory.Exists(folder));
            Assert.Single(Directory.GetFiles(folder, "*.wav"));

            var loaded = ProjectIO.Load(path);
            var clip = Assert.Single(loaded.Audio);
            Assert.Equal("My Song!", clip.Name);
            Assert.Equal(3, clip.StartFrame);
            Assert.Equal(0.8, clip.Volume);
            Assert.NotNull(clip.Data);
            Assert.Equal(data.Samples.Length, clip.Data!.Samples.Length);

            // if the audio folder goes missing, the project still opens and the clip is flagged
            Directory.Delete(folder, recursive: true);
            var broken = Assert.Single(ProjectIO.Load(path).Audio);
            Assert.True(broken.Missing);
            Assert.Null(broken.Data);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
