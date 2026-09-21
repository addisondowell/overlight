namespace Overlight.App.ReadLayer;

/// <summary>
/// A single point of read-only truth for "what is on the desktop right
/// now", combining the window list and notification list. The
/// presentation layer should only ever consume this snapshot — it should
/// never reach into WindowEnumerator/NotificationReader directly, so the
/// "only reads, never mutates" boundary has one place to hold.
/// </summary>
public sealed record DesktopState(
    IReadOnlyList<WindowSnapshot> Windows,
    IReadOnlyList<NotificationSnapshot> Notifications,
    DateTimeOffset CapturedAt);

public sealed class DesktopStateReader
{
    private readonly NotificationReader _notificationReader = new();
    private bool _notificationsReady;

    public async Task<bool> InitializeAsync()
    {
        _notificationsReady = await _notificationReader.TryInitializeAsync();
        return _notificationsReady;
    }

    public async Task<DesktopState> CaptureAsync()
    {
        IReadOnlyList<WindowSnapshot> windows = WindowEnumerator.Snapshot();

        IReadOnlyList<NotificationSnapshot> notifications = _notificationsReady
            ? await _notificationReader.SnapshotAsync()
            : Array.Empty<NotificationSnapshot>();

        return new DesktopState(windows, notifications, DateTimeOffset.Now);
    }
}
