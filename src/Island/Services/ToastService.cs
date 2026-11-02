using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Island.Services;

/// <summary>
/// Real Windows toast notifications (Action Center) for informational events.
/// Reminders keep the custom window because they need action buttons.
/// </summary>
public static class ToastService
{
    public const string AppId = "Island.Widget";

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId);

    public static void Initialize()
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppId);

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + AppId);
            key?.SetValue("DisplayName", "Island");
            key?.SetValue("IconUri", Environment.ProcessPath ?? "");
        }
        catch
        {
        }
    }

    public static bool TryShow(string title, string message)
    {
        try
        {
            var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
            if (notifier.Setting != NotificationSetting.Enabled) return false;

            var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var texts = xml.GetElementsByTagName("text");
            texts[0].AppendChild(xml.CreateTextNode(title));
            texts[1].AppendChild(xml.CreateTextNode(message));

            notifier.Show(new ToastNotification(xml));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
