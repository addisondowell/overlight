using Microsoft.UI.Xaml;

namespace Overlight.App.Presentation;

/// <summary>
/// Reports when the overlay surface is not visible (minimized, or
/// switched away from on a virtual desktop), so AmbientCompositor can
/// stop paying even the one-time compositor cost of an animation nobody
/// can see. Per docs/DESIGN.md section 5 this must be push-driven rather
/// than a polling timer.
///
/// Previously this subclassed the window's raw Win32 procedure
/// (SetWindowLongPtr + DwmGetWindowAttribute(DWMWA_CLOAKED)) to catch DWM
/// cloak transitions directly. Replaced after a real-device report of
/// the whole app running slowly: routing every single window message
/// (mouse move, paint, DPI, etc.) through a managed P/Invoke callback
/// before forwarding it back to WinUI's own window procedure is real
/// overhead, and worse, risks interfering with WinUI 3's internal
/// input/compositor pipeline — exactly the on-device-only risk this
/// class's previous version flagged as unverified when it was written.
///
/// Window.VisibilityChanged is the supported, documented WinUI 3 API for
/// this and carries none of that risk. The trade-off: it's a coarser
/// signal than true DWM cloak state — a window fully covered by another
/// window but not minimized or switched away from still reports
/// "visible" and keeps animating. That's an acceptable cost for removing
/// a real, previously-flagged performance and stability risk.
/// </summary>
public sealed class OcclusionMonitor : IDisposable
{
    private readonly Window _window;
    private bool _lastKnownOccluded;

    public event Action<bool>? OccludedChanged;

    public OcclusionMonitor(Window window)
    {
        _window = window;
    }

    public void Start() => _window.VisibilityChanged += OnVisibilityChanged;

    private void OnVisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        bool occluded = !args.Visible;
        if (occluded == _lastKnownOccluded)
        {
            return;
        }

        _lastKnownOccluded = occluded;
        OccludedChanged?.Invoke(occluded);
    }

    public void Dispose() => _window.VisibilityChanged -= OnVisibilityChanged;
}
