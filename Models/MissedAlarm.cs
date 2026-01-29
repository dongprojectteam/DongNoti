using System;

namespace DongNoti.Models
{
    public class MissedAlarm
    {
        public string AlarmId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime ScheduledTime { get; set; }
        public string RepeatTypeString { get; set; } = string.Empty;

        public MissedAlarm()
        {
        }

        public MissedAlarm(string alarmId, string title, DateTime scheduledTime, string repeatTypeString)
        {
            AlarmId = alarmId;
            Title = title;
            ScheduledTime = scheduledTime;
            RepeatTypeString = repeatTypeString;
        }
    }
}
