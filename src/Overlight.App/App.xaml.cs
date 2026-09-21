using Microsoft.UI.Xaml;

namespace Overlight.App;

/// <summary>
/// Composition root. Owns the single overlay window and wires the three
/// layers together (read -&gt; presentation, suppression as a side toggle).
/// See docs/DESIGN.md for the architecture this mirrors.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
