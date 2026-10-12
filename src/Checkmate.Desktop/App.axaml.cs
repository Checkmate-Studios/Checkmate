using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Checkmate.Desktop.ViewModels;

namespace Checkmate.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splash = new SplashWindow();
            desktop.MainWindow = splash;

            MainWindow? main = null;
            MainWindow Create() => new() { DataContext = new MainViewModel() };

            // Build the editor in the background while the splash plays, so there is no pause afterwards.
            Dispatcher.UIThread.Post(() => main ??= Create(), DispatcherPriority.Background);

            splash.Finished += () =>
            {
                main ??= Create();
                desktop.MainWindow = main;
                main.Show();
                splash.Close();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
