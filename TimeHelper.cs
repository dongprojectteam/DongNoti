using System;

namespace DongNoti
{
    public static class TimeHelper
    {
        public static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}시간 {timeSpan.Minutes}분";
            }

            return $"{(int)timeSpan.TotalMinutes}분";
        }

        public static DateTime ToMinutePrecision(DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
        }
    }
}
