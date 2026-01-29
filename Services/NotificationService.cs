using System;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using DongNoti.Models;

namespace DongNoti.Services
{
    public class NotificationService
    {
        private const string AppId = "DongNoti";

        public void ShowAlarmNotification(Alarm alarm)
        {
            try
            {
                LogService.LogInfo($"Toast 알림 표시 시작: '{alarm.Title}'");
                var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);

                // 제목 설정
                var titleElements = toastXml.GetElementsByTagName("text");
                titleElements[0].AppendChild(toastXml.CreateTextNode("알람"));
                titleElements[1].AppendChild(toastXml.CreateTextNode(alarm.Title));

                // 알람 시간 표시
                var timeText = alarm.DateTime.ToString("yyyy-MM-dd HH:mm");
                if (alarm.RepeatType != RepeatType.None)
                {
                    timeText += $" ({alarm.RepeatTypeString})";
                }

                // Toast 속성 설정
                var toastNode = toastXml.SelectSingleNode("/toast");
                if (toastNode != null)
                {
                    var xmlElement = toastNode as XmlElement;
                    xmlElement?.SetAttribute("duration", "long");
                }

                var toast = new ToastNotification(toastXml);
                toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(5);

                ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);
                LogService.LogInfo($"Toast 알림 표시 완료: '{alarm.Title}'");
            }
            catch (Exception ex)
            {
                LogService.LogError($"알림 표시 실패: '{alarm.Title}'", ex);
            }
        }
    }
}

