using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DongNoti.Models;
using DongNoti.Services;

namespace DongNoti.Views
{
    public partial class MissedAlarmsSummaryWindow : Window
    {
        public MissedAlarmsSummaryWindow()
        {
            InitializeComponent();
            LoadMissedAlarms();
        }

        private void LoadMissedAlarms()
        {
            var missedAlarms = FocusModeService.Instance.GetMissedAlarms();
            
            if (missedAlarms == null || missedAlarms.Count == 0)
            {
                // 놓친 알람이 없는 경우
                SubtitleText.Text = "놓친 알람이 없습니다";
                StatisticsText.Text = "알람이 없었습니다";
                MissedAlarmsListBox.ItemsSource = new List<MissedAlarm>();
                return;
            }

            // 시간 순으로 정렬
            var sortedAlarms = missedAlarms.OrderBy(a => a.ScheduledTime).ToList();
            
            // 통계 업데이트
            StatisticsText.Text = $"총 {missedAlarms.Count}개의 알람을 놓치셨습니다";
            
            // 목록 표시
            MissedAlarmsListBox.ItemsSource = sortedAlarms;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // 놓친 알람 목록 초기화
            FocusModeService.Instance.ClearMissedAlarms();
            Close();
        }
    }
}
