using System.Runtime.InteropServices;

namespace Overlight.App.Presentation;

/// <summary>
/// Reports when the overlay surface itself becomes cloaked/occluded, so
/// AmbientCompositor can stop paying even the one-time compositor cost of
/// an animation nobody can see. Per docs/DESIGN.md section 5 this must be
/// push-driven rather than a polling timer.
///
/// There is no dedicated "cloak state changed" push API for an arbitrary
/// window. The documented-in-practice technique (used by several
/// Microsoft samples and covered on Raymond Chen's blog) is to subclass
/// the window and treat WM_WINDOWPOSCHANGED as the trigger to re-query
/// DwmGetWindowAttribute(DWMWA_CLOAKED) — cloaking transitions reliably
/// produce a WM_WINDOWPOSCHANGED on the affected window as a side effect.
/// That's what this class does: no timer, but still a query, driven off
/// a real window message rather than a schedule.
///
/// NEEDS ON-DEVICE VERIFICATION: confirm WM_WINDOWPOSCHANGED actually
/// fires reliably for the cloak transitions this app cares about
/// (virtual-desktop switch, minimize, UWP suspend) on a current Windows
/// 11 build before leaning on this as the sole throttle signal.
/// </summary>
public sealed class OcclusionMonitor : IDisposable
{
    private const int GWLP_WNDPROC = -4;
    private const int WM_WINDOWPOSCHANGED = 0x0047;
    private const int DWMWA_CLOAKED = 14;

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    private readonly nint _hwnd;
    private readonly WndProcDelegate _wndProc;
    private nint _originalWndProc;
    private bool _lastKnownCloaked;

    public event Action<bool>? CloakedChanged;

    public OcclusionMonitor(nint hwnd)
    {
        _hwnd = hwnd;
        _wndProc = WndProc;
    }

    public void Start()
    {
        _originalWndProc = SetWindowLongPtr(
            _hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_WINDOWPOSCHANGED)
        {
            bool isCloaked = DwmGetWindowAttribute(
                _hwnd, DWMWA_CLOAKED, out int cloakedValue, sizeof(int)) == 0
                && cloakedValue != 0;

            if (isCloaked != _lastKnownCloaked)
            {
                _lastKnownCloaked = isCloaked;
                CloakedChanged?.Invoke(isCloaked);
            }
        }

        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_originalWndProc != nint.Zero)
        {
            SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _originalWndProc);
            _originalWndProc = nint.Zero;
        }
    }
}
