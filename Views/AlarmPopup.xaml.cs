using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using DongNoti.Models;
using DongNoti.Services;

namespace DongNoti.Views
{
    public partial class AlarmPopup : Window
    {
        private readonly Alarm _alarm;
        private readonly SoundService _soundService;
        private DispatcherTimer? _autoDismissTimer;
        private DispatcherTimer? _countdownTimer;
        private DateTime _startTime;
        private int _totalSeconds;

        private readonly bool _isTestMode;

        public AlarmPopup(Alarm alarm, SoundService? soundService = null, bool isTestMode = false)
        {
            InitializeComponent();
            _alarm = alarm;
            _soundService = soundService ?? new SoundService();
            _isTestMode = isTestMode;
            if (isTestMode)
            {
                HeaderTextBlock.Text = "테스트 모드";
            }
            else
            {
                HeaderTextBlock.Text = $"알람 ({alarm.DateTime:yyyy-MM-dd HH:mm})";
            }
            TitleTextBlock.Text = alarm.Title;
            
            if (alarm.RepeatType != RepeatType.None)
            {
                RepeatTextBlock.Text = $"반복: {alarm.RepeatTypeString}";
                RepeatTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                RepeatTextBlock.Visibility = Visibility.Collapsed;
            }
            if (!isTestMode)
            {
                _soundService.PlayAlarmSound(alarm);
            }
            if (!isTestMode)
            {
                StartAutoDismissTimer();
            }
            else
            {
                CountdownPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void StartAutoDismissTimer()
        {
            if (_alarm.AutoDismissMinutes <= 0)
            {
                CountdownPanel.Visibility = Visibility.Collapsed;
                LogService.LogInfo($"알람 '{_alarm.Title}' 자동 종료 비활성화");
                return;
            }

            _startTime = DateTime.Now;
            _totalSeconds = _alarm.AutoDismissMinutes * 60;
            _countdownTimer = new DispatcherTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += UpdateCountdown;
            _countdownTimer.Start();
            _autoDismissTimer = new DispatcherTimer();
            _autoDismissTimer.Interval = TimeSpan.FromMinutes(_alarm.AutoDismissMinutes);
            _autoDismissTimer.Tick += (s, e) =>
            {
                LogService.LogInfo($"알람 '{_alarm.Title}' 자동 종료 ({_alarm.AutoDismissMinutes}분 경과)");
                _autoDismissTimer?.Stop();
                _countdownTimer?.Stop();
                _soundService.StopSound();
                RecordMissedAlarm();
                
                this.Close();
            };
            _autoDismissTimer.Start();
            
            LogService.LogInfo($"알람 '{_alarm.Title}' 자동 종료 타이머 시작: {_alarm.AutoDismissMinutes}분");
            UpdateCountdown(null, null);
        }

        private void UpdateCountdown(object? sender, EventArgs? e)
        {
            var elapsed = (DateTime.Now - _startTime).TotalSeconds;
            var remaining = _totalSeconds - (int)elapsed;

            if (remaining <= 0)
            {
                CountdownProgressBar.Value = 0;
                CountdownTextBlock.Text = "자동 종료 중...";
                return;
            }

            var minutes = remaining / 60;
            var seconds = remaining % 60;

            CountdownProgressBar.Value = (remaining / (double)_totalSeconds) * 100;
            CountdownTextBlock.Text = $"{minutes}분 {seconds}초 후 자동 종료";
        }

        private void Snooze_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _autoDismissTimer?.Stop();
                _countdownTimer?.Stop();
                _soundService.StopSound();
                RecordSuccessfulAlarm();
                _alarm.LastTriggered = DateTime.Now;
                var snoozeAlarm = new Alarm
                {
                    Title = $"{_alarm.Title} (5분 후)",
                    DateTime = DateTime.Now.AddMinutes(5),
                    RepeatType = RepeatType.None,
                    IsEnabled = true,
                    SoundFilePath = _alarm.SoundFilePath,
                    IsTemporary = true
                };
                if (Application.Current is App app && app.AlarmService != null)
                {
                    app.AlarmService.SnoozeAlarm(_alarm.Id, _alarm.LastTriggered.Value, snoozeAlarm);
                }
                var originalAlarm = _alarm;
                if (originalAlarm != null)
                {
                    originalAlarm.LastTriggered = _alarm.LastTriggered;
                    LogService.LogInfo($"원래 알람 '{originalAlarm.Title}' LastTriggered 업데이트: {_alarm.LastTriggered:yyyy-MM-dd HH:mm}");
                }

                LogService.LogInfo($"5분 후 다시 알림 설정: '{snoozeAlarm.Title}' at {snoozeAlarm.DateTime:yyyy-MM-dd HH:mm}");
                try
                {
                    if (Application.Current is App messageApp)
                    {
                        messageApp.Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                MessageBox.Show("5분 후 다시 알림이 설정되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            catch (Exception refreshEx)
                            {
                                LogService.LogError("MainWindow 알람 목록 새로고침 중 오류", refreshEx);
                            }
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
                catch (Exception refreshEx)
                {
                    LogService.LogError("5분 후 알림 설정 후 UI 업데이트 중 오류", refreshEx);
                }
                this.Close();
            }
            catch (Exception ex)
            {
                LogService.LogError("5분 후 다시 알림 설정 중 오류", ex);
                MessageBox.Show($"5분 후 다시 알림 설정 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _autoDismissTimer?.Stop();
                _countdownTimer?.Stop();
                _soundService.StopSound();
                RecordSuccessfulAlarm();
                if (_alarm.RepeatType == RepeatType.None)
                {
                    var now = DateTime.Now;
                    _alarm.LastTriggered = now;
                    if (Application.Current is App app && app.AlarmService != null)
                    {
                        app.AlarmService.SetLastTriggered(_alarm.Id, now);
                        LogService.LogInfo($"알람 '{_alarm.Title}' LastTriggered 설정: {now:yyyy-MM-dd HH:mm}");
                    }
                }
                
                this.Close();
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 끄기 중 오류", ex);
                this.Close();
            }
        }

        /// <summary>
        /// 놓친 알람으로 기록합니다 (자동 종료만)
        /// </summary>
        private void RecordMissedAlarm()
        {
            try
            {
                if (Application.Current is App app && app.AlarmService != null)
                {
                    app.AlarmService.RecordAlarmHistory(_alarm, wasMissed: true);
                    LogService.LogInfo($"알람 '{_alarm.Title}' 놓친 알람으로 기록됨 (자동 종료)");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("놓친 알람 기록 중 오류", ex);
            }
        }

        /// <summary>
        /// 성공한 알람으로 기록합니다 (닫기 버튼 또는 5분 후 다시 알림)
        /// </summary>
        private void RecordSuccessfulAlarm()
        {
            try
            {
                if (Application.Current is App app && app.AlarmService != null)
                {
                    app.AlarmService.RecordAlarmHistory(_alarm, wasMissed: false);
                    LogService.LogInfo($"알람 '{_alarm.Title}' 성공한 알람으로 기록됨 (사용자 처리)");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("성공한 알람 기록 중 오류", ex);
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _autoDismissTimer?.Stop();
            _countdownTimer?.Stop();
            _soundService.StopSound();
            base.OnClosed(e);
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                    this.DragMove();
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"AlarmPopup DragMove 중 예외: {ex.Message}");
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Dismiss_Click(sender, e);
            }
        }
    }
}

