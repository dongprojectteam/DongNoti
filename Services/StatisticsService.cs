using System;
using System.Collections.Generic;
using System.Linq;
using DongNoti.Models;

namespace DongNoti.Services
{
    public class StatisticsService
    {
        /// <summary>
        /// 통계 데이터를 계산합니다.
        /// </summary>
        public static StatisticsData CalculateStatistics(DateTime? startDate = null, DateTime? endDate = null)
        {
            var settings = StorageService.LoadSettings();
            var history = settings.AlarmHistory ?? new List<AlarmHistory>();

            // 날짜 필터 적용
            if (startDate.HasValue || endDate.HasValue)
            {
                history = history.Where(h =>
                {
                    if (startDate.HasValue && h.TriggeredAt < startDate.Value)
                        return false;
                    if (endDate.HasValue && h.TriggeredAt > endDate.Value)
                        return false;
                    return true;
                }).ToList();
            }

            var data = new StatisticsData
            {
                TotalTriggers = history.Count,
                MissedTriggers = history.Count(h => h.WasMissed),
                SuccessfulTriggers = history.Count(h => !h.WasMissed),
                AlarmTriggerCounts = history
                    .GroupBy(h => h.AlarmId)
                    .ToDictionary(
                        g => g.Key,
                        g => new AlarmTriggerInfo
                        {
                            AlarmId = g.Key,
                            AlarmTitle = g.First().AlarmTitle,
                            TriggerCount = g.Count(),
                            MissedCount = g.Count(h => h.WasMissed)
                        }
                    ),
                MostTriggeredAlarm = history
                    .GroupBy(h => h.AlarmId)
                    .OrderByDescending(g => g.Count())
                    .Select(g => new AlarmTriggerInfo
                    {
                        AlarmId = g.Key,
                        AlarmTitle = g.First().AlarmTitle,
                        TriggerCount = g.Count(),
                        MissedCount = g.Count(h => h.WasMissed)
                    })
                    .FirstOrDefault()
            };

            return data;
        }
    }

    public class StatisticsData
    {
        public int TotalTriggers { get; set; }
        public int MissedTriggers { get; set; }
        public int SuccessfulTriggers { get; set; }
        public Dictionary<string, AlarmTriggerInfo> AlarmTriggerCounts { get; set; } = new Dictionary<string, AlarmTriggerInfo>();
        public AlarmTriggerInfo? MostTriggeredAlarm { get; set; }
    }

    public class AlarmTriggerInfo
    {
        public string AlarmId { get; set; } = string.Empty;
        public string AlarmTitle { get; set; } = string.Empty;
        public int TriggerCount { get; set; }
        public int MissedCount { get; set; }
    }
}
