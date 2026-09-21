using System.Runtime.InteropServices;
using System.Text;

namespace Overlight.App.ReadLayer;

/// <summary>
/// Reads the current top-level window list via EnumWindows. Read-only:
/// never posts messages, never moves/resizes/activates a window it did
/// not create itself.
/// </summary>
public static class WindowEnumerator
{
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    // DWMWA_CLOAKED — whether the window is cloaked by DWM (e.g. on another
    // virtual desktop, or a suspended UWP window). Documented in dwmapi.h.
    private const int DWMWA_CLOAKED = 14;

    public static IReadOnlyList<WindowSnapshot> Snapshot()
    {
        var results = new List<WindowSnapshot>();

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            int length = GetWindowTextLength(hWnd);
            string title = string.Empty;
            if (length > 0)
            {
                var sb = new StringBuilder(length + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                title = sb.ToString();
            }

            // Skip windows with no title — almost always helper/owner
            // windows, not something a user would recognize as an app.
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            GetWindowThreadProcessId(hWnd, out uint pid);

            bool isCloaked = DwmGetWindowAttribute(
                hWnd, DWMWA_CLOAKED, out int cloakedValue, sizeof(int)) == 0
                && cloakedValue != 0;

            results.Add(new WindowSnapshot(hWnd, title, (int)pid, IsVisible: true, isCloaked));
            return true;
        }, nint.Zero);

        return results;
    }
}
