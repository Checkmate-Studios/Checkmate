using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Checkmate.Desktop;

/// <summary>Animated start-up splash. Click or press any key to skip.</summary>
public partial class SplashWindow : Window
{
    private const double IntroSeconds = 5.5;   // how long the splash stays on screen: change this one number to taste
    private const double FadeSeconds = 0.6;    // fade-out before the editor appears

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = new();
    private readonly ScaleTransform _logoScale = new(1, 1);
    private readonly TranslateTransform _titleShift = new();
    private readonly TranslateTransform _tagShift = new();
    private bool _done;

    /// <summary>Raised once, when the splash has finished (or was skipped).</summary>
    public event Action? Finished;

    public SplashWindow()
    {
        InitializeComponent();
        LoadLogo();

        LogoHost.RenderTransform = _logoScale;
        TitleText.RenderTransform = _titleShift;
        Tagline.RenderTransform = _tagShift;

        _timer.Tick += (_, _) => OnTick();
        Opened += (_, _) => { _clock.Start(); _timer.Start(); };
        PointerPressed += (_, _) => Finish();
        KeyDown += (_, _) => Finish();
    }

    private void LoadLogo()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Checkmate/Assets/Checkmate.ico"));
            Logo.Source = new Bitmap(stream);
        }
        catch
        {
            Logo.IsVisible = false;       // no icon file (or it could not be decoded): show a chess king instead
            Fallback.IsVisible = true;
        }
    }

    private static double Ease(double x) => 1 - Math.Pow(1 - Math.Clamp(x, 0, 1), 3);   // cubic ease-out
    private static double Step(double t, double start, double length) => Ease((t - start) / length);

    private void OnTick()
    {
        double t = _clock.Elapsed.TotalSeconds;
        Art.SetTime(t);

        double logo = Step(t, 0.3, 1.4);
        LogoHost.Opacity = logo;
        double breathe = 1 + 0.015 * Math.Sin(t * 3);
        _logoScale.ScaleX = _logoScale.ScaleY = (0.7 + 0.3 * logo) * breathe;
        Glow.Opacity = 0.6 + 0.4 * Math.Sin(t * 2.2);

        double title = Step(t, 1.2, 1.4);
        TitleText.Opacity = title;
        TitleText.LetterSpacing = 14 + (1 - title) * 26;      // letters glide together
        _titleShift.Y = (1 - title) * 18;

        double tag = Step(t, 2.4, 1.1);
        Tagline.Opacity = tag;
        _tagShift.Y = (1 - tag) * 10;
        Footer.Opacity = Step(t, 3.0, 1.2);

        Bar.Width = Math.Max(0, Root.Bounds.Width * Ease(t / IntroSeconds));

        if (t >= IntroSeconds)
        {
            double f = (t - IntroSeconds) / FadeSeconds;
            Opacity = Math.Clamp(1 - f, 0, 1);
            if (f >= 1) Finish();
        }
    }

    private void Finish()
    {
        if (_done) return;
        _done = true;
        _timer.Stop();
        Finished?.Invoke();
    }
}
