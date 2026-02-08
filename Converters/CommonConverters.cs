using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DongNoti.Models;

namespace DongNoti.Converters
{
    /// <summary>
    /// Null을 Boolean으로 변환하는 컨버터 (null이면 false, 아니면 true)
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// AlarmType에 따라 Visibility를 변환하는 컨버터
    /// </summary>
    public class AlarmTypeToVisibilityConverter : IValueConverter
    {
        public string TargetType { get; set; } = "Alarm"; // "Alarm" 또는 "Dday"

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AlarmType alarmType)
            {
                if (TargetType == "Dday")
                {
                    return alarmType == AlarmType.Dday ? Visibility.Visible : Visibility.Collapsed;
                }
                else // TargetType == "Alarm"
                {
                    return alarmType == AlarmType.Alarm ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 알람이 지났는지 판별하는 컨버터 (반복 없는 알람 중 GetNextAlarmTime() == null인 경우)
    /// </summary>
    public class IsPastAlarmConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Alarm alarm)
            {
                if (alarm.AlarmType == AlarmType.Alarm)
                {
                    // 반복 없는 알람 중 이미 지난 것
                    return alarm.RepeatType == RepeatType.None && alarm.GetNextAlarmTime() == null;
                }
                else if (alarm.AlarmType == AlarmType.Dday)
                {
                    // Dday는 IsDdayPassed 사용
                    return alarm.IsDdayPassed;
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 메인창용 Dday 표시 문자열 컨버터 (지난 날짜는 D+1 형식으로 표시)
    /// </summary>
    public class MainWindowDdayDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Alarm alarm && alarm.AlarmType == AlarmType.Dday)
            {
                var days = alarm.DaysRemaining;
                if (!days.HasValue)
                    return string.Empty;

                if (days.Value < 0)
                {
                    // 지난 날짜는 D+1, D+2 형식으로 표시
                    return $"D+{Math.Abs(days.Value)}";
                }
                else if (days.Value == 0)
                {
                    return "D-day";
                }
                else
                {
                    return $"D-{days.Value}";
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
