using System.Windows;

namespace Reverie;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string arg = e.Args.Length > 0 ? e.Args[0].ToLower().Trim().Substring(0, 2) : "/s";

        switch (arg)
        {
            case "/c":
                MessageBox.Show("Screensaver Settings: Nothing to configure yet!", "Music Screensaver");
                Current.Shutdown();
                break;
            case "/p":
                // Preview mode (in the tiny Windows settings box) - ignored for now
                Current.Shutdown();
                break;
            case "/s":
            default:
                // Start fullscreen screensaver
                var window = new MainWindow();
                window.Show();
                break;
        }
    }
}