using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Checkmate.Core.Model;
using SkiaSharp;

namespace Checkmate.Desktop;

/// <summary>"Add text" dialog: text, font, size, bold, italic. Remembers the last choices.</summary>
public static class TextDialog
{
    private static string? _font;
    private static decimal _size = 64;
    private static bool _bold, _italic;

    public static async Task<TextItem?> ShowAsync(Window owner)
    {
        TextItem? result = null;

        var families = SKFontManager.Default.GetFontFamilies().OrderBy(f => f).ToList();
        string defaultFont = _font
            ?? families.FirstOrDefault(f => f == "Arial")
            ?? families.FirstOrDefault(f => f.Contains("Sans"))
            ?? families.FirstOrDefault()
            ?? "Arial";

        var dialog = new Window
        {
            Title = "Add text",
            Width = 540,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var box = new TextBox { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 110, Watermark = "Type your text here..." };
        var font = new ComboBox { ItemsSource = families, SelectedItem = defaultFont, HorizontalAlignment = HorizontalAlignment.Stretch };
        var size = new NumericUpDown { Minimum = 6, Maximum = 800, Increment = 4, Value = _size, FormatString = "0", Width = 120 };
        var bold = new CheckBox { Content = "Bold", IsChecked = _bold };
        var italic = new CheckBox { Content = "Italic", IsChecked = _italic };
        var add = new Button { Content = "Add text", MinWidth = 100, IsEnabled = false };
        var cancel = new Button { Content = "Cancel", MinWidth = 90 };

        box.TextChanged += (_, _) => add.IsEnabled = !string.IsNullOrWhiteSpace(box.Text);
        cancel.Click += (_, _) => dialog.Close();
        add.Click += (_, _) =>
        {
            _font = font.SelectedItem as string ?? defaultFont;
            _size = size.Value ?? 64;
            _bold = bold.IsChecked == true;
            _italic = italic.IsChecked == true;
            result = new TextItem
            {
                Value = box.Text ?? "",
                FontFamily = _font,
                FontSize = (float)_size,
                Bold = _bold,
                Italic = _italic,
            };
            dialog.Close();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Text" },
                box,
                new TextBlock { Text = "Font" },
                font,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 14,
                    Children = { new TextBlock { Text = "Size", VerticalAlignment = VerticalAlignment.Center }, size, bold, italic },
                },
                new TextBlock { Text = "Uses your current colour and opacity. The text is placed where you clicked.", Opacity = 0.6, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8,
                    Children = { cancel, add },
                },
            },
        };
        dialog.Opened += (_, _) => box.Focus();

        await dialog.ShowDialog(owner);
        return result;
    }
}
