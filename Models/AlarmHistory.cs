using System;

namespace DongNoti.Models
{
    /// <summary>
    /// 알람 트리거 히스토리
    /// </summary>
    public class AlarmHistory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AlarmId { get; set; } = string.Empty;
        public string AlarmTitle { get; set; } = string.Empty;
        public DateTime TriggeredAt { get; set; } = DateTime.Now;
        public bool WasMissed { get; set; } = false; // 집중모드에서 놓친 알람인지 여부
    }
}
