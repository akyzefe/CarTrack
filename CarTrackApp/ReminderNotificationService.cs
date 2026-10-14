using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;

namespace CarTrackApp;

internal static class ReminderNotificationService
{
#if IOS || ANDROID
    private static Task<bool>? _permissionRequest;
#endif

    public static async Task<bool> ScheduleAsync(int id, string title, string description, DateTime dueDate)
    {
#if IOS || ANDROID
        if (!await EnsurePermissionAsync())
            return false;

        var notifyTime = dueDate.Date.AddDays(-30).AddHours(9);
        if (dueDate.Date < DateTime.Today)
        {
            Cancel(id);
            return true;
        }

        if (notifyTime <= DateTime.Now)
        {
            var key = $"reminder.date.notified.{id}.{dueDate:yyyyMMdd}";
            if (Preferences.Default.Get(key, false))
                return true;

            notifyTime = DateTime.Now.AddSeconds(5);
            Preferences.Default.Set(key, true);
        }

        Show(id, title, description, notifyTime);

        return true;
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    public static async Task<bool> ShowNowAsync(int id, string title, string description)
    {
#if IOS || ANDROID
        if (!await EnsurePermissionAsync())
            return false;

        Show(id, title, description, DateTime.Now.AddSeconds(5));
        return true;
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    public static void Cancel(int id)
    {
#if IOS || ANDROID
        LocalNotificationCenter.Current.Cancel(id);
#endif
    }

#if IOS || ANDROID
    private static async Task<bool> EnsurePermissionAsync()
    {
        _permissionRequest ??= LocalNotificationCenter.Current.RequestNotificationPermission();
        return await _permissionRequest;
    }

    private static void Show(int id, string title, string description, DateTime notifyTime)
    {
        LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = id,
            Title = title,
            Description = description,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyTime
            }
        });
    }
#endif
}
