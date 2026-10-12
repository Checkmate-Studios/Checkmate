using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Checkmate.Desktop.Controls;

/// <summary>
/// The animated splash backdrop: a drifting, pulsing chessboard, anime-style speed lines radiating
/// from the logo, a soft purple/pink glow and HUD corner brackets. Pure drawing code, no image files.
/// </summary>
public sealed class SplashArt : Control
{
    private static GradientStop Stop(string hex, double offset) => new() { Color = Color.Parse(hex), Offset = offset };

    private static readonly IBrush Backdrop = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { Stop("#090912", 0), Stop("#1A0F33", 0.6), Stop("#2B1247", 1) },
    };

    private static readonly IBrush Shade = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { Stop("#96000000", 0), Stop("#00000000", 0.3), Stop("#00000000", 0.7), Stop("#C8000000", 1) },
    };

    private static readonly IBrush?[] WhiteCache = new IBrush?[256];
    private static IBrush White(byte alpha) =>
        WhiteCache[alpha] ??= new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));

    public double Time { get; private set; }

    public void SetTime(double seconds)
    {
        Time = seconds;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w < 1 || h < 1) return;

        double t = Time;
        double intro = Math.Clamp(t / 1.6, 0, 1);
        var center = new Point(w / 2, h / 2 - 26);

        ctx.FillRectangle(Backdrop, new Rect(0, 0, w, h));

        // ---- drifting chessboard ----
        const double S = 44;
        double ox = -((t * 14) % (S * 2)) - S * 2;
        int cols = (int)(w / S) + 5, rows = (int)(h / S) + 3;
        for (int r = 0; r < rows; r++)
        {
            for (int q = 0; q < cols; q++)
            {
                if (((q + r) & 1) != 0) continue;
                double x = ox + q * S, y = r * S;
                double dx = (x + S / 2 - center.X) / (w / 2), dy = (y + S / 2 - center.Y) / (h / 2);
                double d = Math.Sqrt(dx * dx + dy * dy);
                double a = (0.018 + 0.075 * (0.5 + 0.5 * Math.Sin(t * 1.7 - (q + r) * 0.45)))
                           * Math.Clamp(1.25 - d * 0.8, 0, 1) * intro;
                byte alpha = (byte)(a * 255);
                if (alpha < 2) continue;
                ctx.FillRectangle(White(alpha), new Rect(x + 1, y + 1, S - 2, S - 2), 4);
            }
        }

        // ---- glow (stacked translucent ellipses = soft radial gradient) ----
        double pulse = 0.85 + 0.15 * Math.Sin(t * 2.2);
        for (int i = 0; i < 22; i++)
        {
            double k = i / 22.0, rad = 60 + i * 15;
            byte a = (byte)Math.Clamp(9 * (1 - k) * pulse * intro, 0, 255);
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(a, 108, 92, 231)), null, center, rad, rad * 0.86);
        }
        for (int i = 0; i < 10; i++)
        {
            double k = i / 10.0, rad = 40 + i * 11;
            byte a = (byte)Math.Clamp(7 * (1 - k) * pulse * intro, 0, 255);
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(a, 255, 77, 157)), null, center, rad, rad);
        }

        // ---- anime speed lines ----
        const int lines = 44;
        for (int k = 0; k < lines; k++)
        {
            uint hash = unchecked((uint)(k * 2654435761L));
            double hv = ((hash >> 8) & 255) / 255.0;
            double angle = k * (Math.PI * 2 / lines) + t * 0.06 + hv * 0.05;
            double breathe = 0.7 + 0.3 * Math.Sin(t * 2.1 + k * 0.9);
            double r1 = 92 + hv * 40;
            double r2 = r1 + (120 + hv * 300) * breathe * intro;
            byte alpha = (byte)((0.04 + 0.11 * hv) * intro * 255);
            var pen = new Pen(White(alpha), 0.8 + hv * 1.6, lineCap: PenLineCap.Round);
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            ctx.DrawLine(pen,
                new Point(center.X + cos * r1, center.Y + sin * r1 * 0.86),
                new Point(center.X + cos * r2, center.Y + sin * r2 * 0.86));
        }

        ctx.FillRectangle(Shade, new Rect(0, 0, w, h));

        // ---- HUD corner brackets drawing themselves in ----
        double m = 22, len = 34 * intro;
        var bracket = new Pen(White((byte)(120 * intro)), 1.5);
        ctx.DrawLine(bracket, new Point(m, m), new Point(m + len, m));
        ctx.DrawLine(bracket, new Point(m, m), new Point(m, m + len));
        ctx.DrawLine(bracket, new Point(w - m, m), new Point(w - m - len, m));
        ctx.DrawLine(bracket, new Point(w - m, m), new Point(w - m, m + len));
        ctx.DrawLine(bracket, new Point(m, h - m), new Point(m + len, h - m));
        ctx.DrawLine(bracket, new Point(m, h - m), new Point(m, h - m - len));
        ctx.DrawLine(bracket, new Point(w - m, h - m), new Point(w - m - len, h - m));
        ctx.DrawLine(bracket, new Point(w - m, h - m), new Point(w - m, h - m - len));
    }
}
