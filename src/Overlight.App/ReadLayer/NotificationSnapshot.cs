namespace Overlight.App.ReadLayer;

/// <summary>
/// Read-only view of a notification already sitting in Action Center.
/// The read layer never dismisses or otherwise mutates the notification
/// it describes — that stays under the user's/OS's control.
/// </summary>
public sealed record NotificationSnapshot(
    uint Id,
    string AppDisplayName,
    string Title,
    string Body,
    DateTimeOffset CreationTime);
