namespace Overclutch.Core.Native;

public static class UserNotificationState
{
    private const int AcceptsNotificationsState = 5; // QUNS_ACCEPTS_NOTIFICATIONS
    private const int QuietTime = 6; // QUNS_QUIET_TIME: first hour after install, only hides notifications that ask for it

    /// <summary>False while a full-screen game, a presentation or a locked session would make Windows drop a notification.</summary>
    public static bool AcceptsNotifications() =>
        NativeMethods.SHQueryUserNotificationState(out var state) != 0 || state is AcceptsNotificationsState or QuietTime;
}
