using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Overlight.App.ReadLayer;

/// <summary>
/// Reads what is already sitting in Action Center via
/// <see cref="UserNotificationListener"/>. This is the API that makes
/// "hide and represent content" possible on Windows without intercepting
/// or blocking anything — the OS still owns delivery, we only read the
/// result.
///
/// IMPORTANT: UserNotificationListener requires the app to carry package
/// identity (a full MSIX package, or a sparse package for an otherwise
/// unpackaged win32/WinUI app) and the user must grant access via
/// RequestAccessAsync the first time. The current .csproj is set to
/// WindowsPackageType=None for a fast unpackaged dev loop; this class
/// will need the app packaged (or a sparse manifest registered) before
/// RequestAccessAsync will succeed. Tracked as a follow-up, not solved
/// here.
/// </summary>
public sealed class NotificationReader
{
    private UserNotificationListener? _listener;

    public async Task<bool> TryInitializeAsync()
    {
        _listener = UserNotificationListener.Current;

        UserNotificationListenerAccessStatus accessStatus =
            await _listener.RequestAccessAsync();

        return accessStatus == UserNotificationListenerAccessStatus.Allowed;
    }

    public async Task<IReadOnlyList<NotificationSnapshot>> SnapshotAsync()
    {
        if (_listener is null)
        {
            throw new InvalidOperationException(
                $"Call {nameof(TryInitializeAsync)} before reading notifications.");
        }

        IReadOnlyList<UserNotification> notifications =
            await _listener.GetNotificationsAsync(NotificationKinds.Toast);

        var results = new List<NotificationSnapshot>(notifications.Count);

        foreach (UserNotification notification in notifications)
        {
            NotificationBinding? binding =
                notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);

            string title = string.Empty;
            string body = string.Empty;

            if (binding is not null)
            {
                IReadOnlyList<AdaptiveNotificationText> texts = binding.GetTextElements();
                title = texts.Count > 0 ? texts[0].Text : string.Empty;
                body = texts.Count > 1 ? string.Join(" ", texts.Skip(1).Select(t => t.Text)) : string.Empty;
            }

            results.Add(new NotificationSnapshot(
                notification.Id,
                notification.AppInfo?.DisplayInfo?.DisplayName ?? string.Empty,
                title,
                body,
                notification.CreationTime));
        }

        return results;
    }
}
