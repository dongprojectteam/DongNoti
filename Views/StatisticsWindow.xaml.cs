using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DongNoti.Services;

namespace DongNoti.Views
{
    public partial class StatisticsWindow : Window
    {
        public StatisticsWindow()
        {
            InitializeComponent();
            Loaded += StatisticsWindow_Loaded;
        }

        private void StatisticsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStatistics();
        }

        private void LoadStatistics()
        {
            try
            {
                var periodItem = PeriodComboBox?.SelectedItem as ComboBoxItem;
                var periodTag = periodItem?.Tag?.ToString() ?? "All";

                DateTime? startDate = null;
                DateTime? endDate = null;

                switch (periodTag)
                {
                    case "Today":
                        startDate = DateTime.Today;
                        endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                        break;
                    case "ThisWeek":
                        var today = DateTime.Today;
                        var daysUntilMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                        startDate = today.AddDays(-daysUntilMonday);
                        endDate = DateTime.Now;
                        break;
                    case "ThisMonth":
                        startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                        endDate = DateTime.Now;
                        break;
                }

                var stats = StatisticsService.CalculateStatistics(startDate, endDate);

                // 요약 통계 업데이트
                if (TotalTriggersText != null)
                    TotalTriggersText.Text = stats.TotalTriggers.ToString();
                if (SuccessfulTriggersText != null)
                    SuccessfulTriggersText.Text = stats.SuccessfulTriggers.ToString();
                if (MissedTriggersText != null)
                    MissedTriggersText.Text = stats.MissedTriggers.ToString();

                // 알람별 통계 업데이트
                if (AlarmStatsDataGrid != null)
                {
                    var alarmStats = stats.AlarmTriggerCounts.Values
                        .OrderByDescending(a => a.TriggerCount)
                        .ToList();

                    AlarmStatsDataGrid.ItemsSource = alarmStats;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("통계 로드 중 오류", ex);
                MessageBox.Show($"통계를 불러오는 중 오류가 발생했습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void PeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadStatistics();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
