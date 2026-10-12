using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Checkmate.Desktop;

public static class Dialogs
{
    /// <summary>A simple modal message. Returns true if the main button was pressed. Pass cancelText = null for an info box.</summary>
    public static async Task<bool> AskAsync(Window owner, string title, string message,
                                            string okText = "OK", string? cancelText = "Cancel")
    {
        bool result = false;
        var dialog = new Window
        {
            Title = title,
            Width = 540,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var ok = new Button { Content = okText, MinWidth = 90 };
        ok.Click += (_, _) => { result = true; dialog.Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        if (cancelText != null)
        {
            var cancel = new Button { Content = cancelText, MinWidth = 90 };
            cancel.Click += (_, _) => dialog.Close();
            buttons.Children.Add(cancel);
        }
        buttons.Children.Add(ok);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 18,
            Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, buttons },
        };

        await dialog.ShowDialog(owner);
        return result;
    }

    /// <summary>
    /// "Which frames to export?" Returns 1-based inclusive (From, To), or null if cancelled.
    /// </summary>
    public static async Task<(int From, int To)?> AskRangeAsync(Window owner, string title, int frameCount, int fps, int currentFrame)
    {
        (int From, int To)? result = null;
        var dialog = new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var fromBox = new NumericUpDown { Minimum = 1, Maximum = frameCount, Value = 1, Increment = 1, FormatString = "0", Width = 120 };
        var toBox = new NumericUpDown { Minimum = 1, Maximum = frameCount, Value = frameCount, Increment = 1, FormatString = "0", Width = 120 };
        var info = new TextBlock { Opacity = 0.75 };
        var export = new Button { Content = "Export", MinWidth = 90 };
        var cancel = new Button { Content = "Cancel", MinWidth = 90 };

        void Refresh()
        {
            int a = (int)(fromBox.Value ?? 1), b = (int)(toBox.Value ?? frameCount);
            bool valid = a <= b;
            export.IsEnabled = valid;
            info.Text = valid
                ? $"{b - a + 1} frames  =  {(b - a + 1) / (double)Math.Max(1, fps):0.0} seconds at {fps} fps"
                : "\"From\" must not be after \"To\".";
        }
        fromBox.ValueChanged += (_, _) => Refresh();
        toBox.ValueChanged += (_, _) => Refresh();

        var whole = new Button { Content = "Whole animation" };
        whole.Click += (_, _) => { fromBox.Value = 1; toBox.Value = frameCount; };
        var untilNow = new Button { Content = $"Up to current frame ({currentFrame})" };
        untilNow.Click += (_, _) => { fromBox.Value = 1; toBox.Value = currentFrame; };

        export.Click += (_, _) =>
        {
            result = ((int)(fromBox.Value ?? 1), (int)(toBox.Value ?? frameCount));
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = $"The animation has {frameCount} frames. Which ones should be exported?", TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = "From frame", VerticalAlignment = VerticalAlignment.Center }, fromBox,
                        new TextBlock { Text = "to frame", VerticalAlignment = VerticalAlignment.Center }, toBox,
                    },
                },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { whole, untilNow } },
                info,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8,
                    Children = { cancel, export },
                },
            },
        };
        Refresh();

        await dialog.ShowDialog(owner);
        return result;
    }
}
