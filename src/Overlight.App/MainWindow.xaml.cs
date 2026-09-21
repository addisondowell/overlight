using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Overlight.App.Presentation;
using Overlight.App.ReadLayer;
using WinRT.Interop;

namespace Overlight.App;

/// <summary>
/// The single presentation-layer surface. This is deliberately thin: it
/// owns wiring the read layer into view and handing throttle signals
/// (occlusion, power mode) to the ambient compositor. It does not itself
/// decide desktop-state, that stays in ReadLayer.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly DesktopStateReader _stateReader = new();
    private readonly PowerModeMonitor _powerModeMonitor = new();
    private AmbientCompositor? _ambientCompositor;
    private OcclusionMonitor? _occlusionMonitor;

    public MainWindow()
    {
        InitializeComponent();
        Activated += OnActivated;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // Only wire up once — Activated fires on every focus change.
        Activated -= OnActivated;

        nint hwnd = WindowNative.GetWindowHandle(this);

        _occlusionMonitor = new OcclusionMonitor(hwnd);
        _occlusionMonitor.CloakedChanged += cloaked => _ambientCompositor?.SetOccluded(cloaked);
        _occlusionMonitor.Start();

        _powerModeMonitor.ModeChanged += mode =>
            _ambientCompositor?.SetLowPower(mode == EffectivePowerMode.BatterySaver);
        _powerModeMonitor.TryStart();

        _ambientCompositor = new AmbientCompositor(RootGrid);
        Visual statusVisual = ElementCompositionPreview.GetElementVisual(StatusText);
        _ambientCompositor.StartBreathingOpacity(statusVisual, from: 0.55f, to: 0.9f, TimeSpan.FromSeconds(3));

        bool notificationsGranted = await _stateReader.InitializeAsync();
        StatusText.Text = notificationsGranted
            ? "Overlight — reading window + notification state"
            : "Overlight — reading window state (notification access not granted)";

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        DesktopState state = await _stateReader.CaptureAsync();
        WindowCountText.Text = $"{state.Windows.Count} visible windows · {state.Notifications.Count} notifications";
    }
}
