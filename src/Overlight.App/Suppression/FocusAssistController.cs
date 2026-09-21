using Microsoft.Win32;

namespace Overlight.App.Suppression;

public enum FocusAssistMode
{
    Off = 0,
    PriorityOnly = 1,
    AlarmsOnly = 2,
}

/// <summary>
/// Toggles the OS's own Focus Assist / Do Not Disturb state. This is
/// intentionally the *only* mutation the app performs — everything else
/// in the app is read-only (see docs/DESIGN.md, section 2). Suppressing
/// banners/sound never drops the notification itself; it still lands in
/// Action Center where NotificationReader can see it.
///
/// CAVEAT: Windows does not expose a public, documented API to set Focus
/// Assist state. The only known lever is writing the
/// CurrentUserFocusAssistConfig value under this registry key, which is
/// how a handful of existing third-party utilities do it — but it is
/// undocumented and could change or break between Windows builds. Treat
/// this class as a best-effort implementation that needs verification on
/// a real Windows install before being trusted, not a confirmed-stable
/// API surface.
/// </summary>
public sealed class FocusAssistController
{
    private const string KeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current" +
        @"\windows.data.notifications.quiethoursprofile\Current";

    public FocusAssistMode? TryGetCurrentMode()
    {
        // TODO: verify the exact value name/shape against a live registry
        // dump on a current Windows 11 build before relying on this.
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        if (key?.GetValue("Data") is not byte[] data || data.Length == 0)
        {
            return null;
        }

        // The QuietHoursProfile blob encodes mode in a known-but-undocumented
        // offset; left unimplemented pending verification rather than
        // guessed at here.
        return null;
    }

    public bool TrySetMode(FocusAssistMode mode)
    {
        // Deliberately unimplemented: writing this registry value from
        // this process without having verified the exact blob format on a
        // real Windows install risks corrupting the user's quiet-hours
        // profile, which cuts directly against the "touch less" principle
        // this whole app is built on. Wire this up once the format has
        // been confirmed against a live system, ideally by diffing the
        // registry before/after toggling Focus Assist from Settings.
        throw new NotImplementedException(
            "FocusAssistController.TrySetMode needs the QuietHoursProfile " +
            "blob format verified on a live Windows install before it is safe to write.");
    }
}
