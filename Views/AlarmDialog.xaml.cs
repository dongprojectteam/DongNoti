using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using DongNoti.Models;
using DongNoti.Services;

namespace DongNoti.Views
{
    public partial class AlarmDialog : Window
    {
        public Alarm? Alarm { get; private set; }
        private Alarm? _existingAlarm;

        /// <param name="existingAlarm">편집할 알람 또는 복제 시 내용을 채울 알람</param>
        /// <param name="forClone">true면 복제 모드: 폼만 채우고 저장 시 새 ID로 추가</param>
        public AlarmDialog(Alarm? existingAlarm = null, bool forClone = false)
        {
            InitializeComponent();
            HourComboBox.ItemsSource = Enumerable.Range(0, 24).Select(i => i.ToString("00"));
            MinuteComboBox.ItemsSource = Enumerable.Range(0, 60).Select(i => i.ToString("00"));
            UpdateUIForType();
            _existingAlarm = forClone ? null : existingAlarm;
            if (existingAlarm != null)
            {
                TitleTextBox.Text = existingAlarm.Title;
                var alarmType = existingAlarm.AlarmType;
                TypeComboBox.SelectedIndex = alarmType == AlarmType.Dday ? 1 : 0;
                UpdateUIForType();
                
                if (alarmType == AlarmType.Dday)
                {
                    if (existingAlarm.TargetDate.HasValue)
                    {
                        DatePicker.SelectedDate = existingAlarm.TargetDate.Value.Date;
                    }
                    else
                    {
                        DatePicker.SelectedDate = DateTime.Today;
                    }
                    RepeatComboBox.SelectedIndex = existingAlarm.RepeatType == RepeatType.Yearly ? 1 : 0;
                }
                else
                {
                    DatePicker.SelectedDate = existingAlarm.DateTime.Date;
                    HourComboBox.SelectedItem = existingAlarm.DateTime.Hour.ToString("00");
                    MinuteComboBox.SelectedItem = existingAlarm.DateTime.Minute.ToString("00");
                    RepeatComboBox.SelectedIndex = (int)existingAlarm.RepeatType;
                    SoundFilePathTextBox.Text = existingAlarm.SoundFilePath ?? "";
                    AutoDismissMinutesTextBox.Text = existingAlarm.AutoDismissMinutes.ToString();
                }
                if (existingAlarm.SelectedDaysOfWeek != null && existingAlarm.SelectedDaysOfWeek.Any())
                {
                    MondayCheckBox.IsChecked = existingAlarm.SelectedDaysOfWeek.Contains(DayOfWeek.Monday);
                    TuesdayCheckBox.IsChecked = existingAlarm.SelectedDaysOfWeek.Contains(DayOfWeek.Tuesday);
                    WednesdayCheckBox.IsChecked = existingAlarm.SelectedDaysOfWeek.Contains(DayOfWeek.Wednesday);
                    ThursdayCheckBox.IsChecked = existingAlarm.SelectedDaysOfWeek.Contains(DayOfWeek.Thursday);
                    FridayCheckBox.IsChecked = existingAlarm.SelectedDaysOfWeek.Contains(DayOfWeek.Friday);
                    SaturdayCheckBox.IsChecked = existingAlarm.SelectedDaysOfWeek.Contains(DayOfWeek.Saturday);
                    SundayCheckBox.IsChecked = existingAlarm.SelectedDaysOfWeek.Contains(DayOfWeek.Sunday);
                }
                else if (existingAlarm.RepeatType == RepeatType.Weekly)
                {
                    var dayOfWeek = existingAlarm.DateTime.DayOfWeek;
                    SetDayCheckBox(dayOfWeek, true);
                }
            }
            else
            {
                TypeComboBox.SelectedIndex = 0;
                TitleTextBox.Text = "알람";
                DatePicker.SelectedDate = DateTime.Today;
                HourComboBox.SelectedItem = DateTime.Now.AddHours(1).Hour.ToString("00");
                MinuteComboBox.SelectedItem = "00";
                RepeatComboBox.SelectedIndex = 0;
                AutoDismissMinutesTextBox.Text = "1";
            }
            LoadCategories();
            if (existingAlarm != null)
            {
                var category = existingAlarm.Category ?? "기본";
                foreach (ComboBoxItem item in CategoryComboBox.Items)
                {
                    if (item.Tag?.ToString() == category)
                    {
                        CategoryComboBox.SelectedItem = item;
                        break;
                    }
                }
                if (CategoryComboBox.SelectedItem == null)
                {
                    var defaultItem = CategoryComboBox.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == "기본");
                    if (defaultItem != null)
                    {
                        CategoryComboBox.SelectedItem = defaultItem;
                    }
                }
                var priorityString = existingAlarm.Priority.ToString();
                foreach (ComboBoxItem item in PriorityComboBox.Items)
                {
                    if (item.Tag?.ToString() == priorityString)
                    {
                        PriorityComboBox.SelectedItem = item;
                        break;
                    }
                }
                if (PriorityComboBox.SelectedItem == null)
                {
                    PriorityComboBox.SelectedIndex = 1;
                }
                if (existingAlarm.AlarmType == AlarmType.Dday && MemoTextBox != null)
                {
                    MemoTextBox.Text = existingAlarm.Memo ?? string.Empty;
                }
            }
            this.Loaded += (s, e) => UpdateDaysOfWeekVisibility();
            UpdateDaysOfWeekVisibility();
        }

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUIForType();
            if (TypeComboBox != null && TitleTextBox != null)
            {
                var selectedItem = TypeComboBox.SelectedItem as ComboBoxItem;
                var typeTag = selectedItem?.Tag?.ToString();
                var isDday = typeTag == "Dday";
                if (TitleTextBox.Text == "알람" || TitleTextBox.Text == "Dday")
                {
                    TitleTextBox.Text = isDday ? "Dday" : "알람";
                }
            }
        }

        private void UpdateUIForType()
        {
            if (TypeComboBox == null)
                return;

            var selectedItem = TypeComboBox.SelectedItem as ComboBoxItem;
            var typeTag = selectedItem?.Tag?.ToString();
            var isDday = typeTag == "Dday";
            if (TimePanel != null)
            {
                TimePanel.Visibility = isDday ? Visibility.Collapsed : Visibility.Visible;
            }
            if (RepeatPanel != null && RepeatComboBox != null)
            {
                RepeatPanel.Visibility = Visibility.Visible;
                RepeatComboBox.Items.Clear();
                if (isDday)
                {
                    RepeatComboBox.Items.Add(new ComboBoxItem { Content = "없음", Tag = "None" });
                    RepeatComboBox.Items.Add(new ComboBoxItem { Content = "매년", Tag = "Yearly" });
                    RepeatComboBox.SelectedIndex = 0;
                }
                else
                {
                    RepeatComboBox.Items.Add(new ComboBoxItem { Content = "없음", Tag = "None" });
                    RepeatComboBox.Items.Add(new ComboBoxItem { Content = "매일", Tag = "Daily" });
                    RepeatComboBox.Items.Add(new ComboBoxItem { Content = "매주", Tag = "Weekly" });
                    RepeatComboBox.Items.Add(new ComboBoxItem { Content = "매월", Tag = "Monthly" });
                    RepeatComboBox.Items.Add(new ComboBoxItem { Content = "매년", Tag = "Yearly" });
                    RepeatComboBox.SelectedIndex = 0;
                }
            }
            if (SoundPanel != null)
            {
                SoundPanel.Visibility = isDday ? Visibility.Collapsed : Visibility.Visible;
            }
            if (AutoDismissPanel != null)
            {
                AutoDismissPanel.Visibility = isDday ? Visibility.Collapsed : Visibility.Visible;
            }
            if (MemoPanel != null)
            {
                MemoPanel.Visibility = isDday ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void LoadCategories()
        {
            try
            {
                var settings = StorageService.LoadSettings();
                var categories = settings.AlarmCategories ?? AppSettings.GetDefaultAlarmCategories();

                CategoryComboBox.Items.Clear();
                if (!categories.Contains("기본"))
                {
                    categories.Insert(0, "기본");
                }
                var sortedCategories = new List<string> { "기본" };
                sortedCategories.AddRange(categories.Where(c => c != "기본"));
                
                bool isFirst = true;
                foreach (var category in sortedCategories)
                {
                    var item = new ComboBoxItem
                    {
                        Content = category,
                        Tag = category,
                        IsSelected = isFirst && category == "기본" // "기본"을 기본 선택
                    };
                    CategoryComboBox.Items.Add(item);
                    isFirst = false;
                }
                
                if (CategoryComboBox.SelectedItem == null)
                {
                    var defaultItem = CategoryComboBox.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == "기본");
                    if (defaultItem != null)
                    {
                        CategoryComboBox.SelectedItem = defaultItem;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("카테고리 목록 로드 중 오류", ex);
            }
        }

        private void SetDayCheckBox(DayOfWeek dayOfWeek, bool isChecked)
        {
            switch (dayOfWeek)
            {
                case DayOfWeek.Monday: MondayCheckBox.IsChecked = isChecked; break;
                case DayOfWeek.Tuesday: TuesdayCheckBox.IsChecked = isChecked; break;
                case DayOfWeek.Wednesday: WednesdayCheckBox.IsChecked = isChecked; break;
                case DayOfWeek.Thursday: ThursdayCheckBox.IsChecked = isChecked; break;
                case DayOfWeek.Friday: FridayCheckBox.IsChecked = isChecked; break;
                case DayOfWeek.Saturday: SaturdayCheckBox.IsChecked = isChecked; break;
                case DayOfWeek.Sunday: SundayCheckBox.IsChecked = isChecked; break;
            }
        }

        private void RepeatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DaysOfWeekPanel != null && RepeatComboBox != null)
            {
                UpdateDaysOfWeekVisibility();
            }
        }

        private void UpdateDaysOfWeekVisibility()
        {
            try
            {
                if (RepeatComboBox == null || DaysOfWeekPanel == null)
                {
                    LogService.LogWarning("UpdateDaysOfWeekVisibility: RepeatComboBox 또는 DaysOfWeekPanel이 null");
                    return;
                }

                var selectedTag = (RepeatComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                LogService.LogDebug($"UpdateDaysOfWeekVisibility 호출: SelectedTag={selectedTag}");

                if (selectedTag == "Weekly")
                {
                    DaysOfWeekPanel.Visibility = Visibility.Visible;
                    LogService.LogInfo("요일 선택 패널 표시됨");
                    bool hasAnyChecked = (MondayCheckBox?.IsChecked == true) ||
                                        (TuesdayCheckBox?.IsChecked == true) ||
                                        (WednesdayCheckBox?.IsChecked == true) ||
                                        (ThursdayCheckBox?.IsChecked == true) ||
                                        (FridayCheckBox?.IsChecked == true) ||
                                        (SaturdayCheckBox?.IsChecked == true) ||
                                        (SundayCheckBox?.IsChecked == true);
                    
                    if (!hasAnyChecked && DatePicker != null)
                    {
                        var selectedDate = DatePicker.SelectedDate ?? DateTime.Today;
                        SetDayCheckBox(selectedDate.DayOfWeek, true);
                        LogService.LogDebug($"요일 자동 선택: {selectedDate.DayOfWeek}");
                    }
                }
                else
                {
                    DaysOfWeekPanel.Visibility = Visibility.Collapsed;
                    LogService.LogDebug($"요일 선택 패널 숨김 (SelectedTag={selectedTag})");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("UpdateDaysOfWeekVisibility 중 오류", ex);
            }
        }

        private void BrowseSoundFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "오디오 파일 (*.wav;*.mp3;*.m4a)|*.wav;*.mp3;*.m4a|모든 파일 (*.*)|*.*",
                Title = "사운드 파일 선택"
            };

            if (dialog.ShowDialog() == true)
            {
                SoundFilePathTextBox.Text = dialog.FileName;
            }
        }

        private void UseDefaultSound_Click(object sender, RoutedEventArgs e)
        {
            SoundFilePathTextBox.Text = "";
        }

        private void TestSound_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var soundFilePath = string.IsNullOrWhiteSpace(SoundFilePathTextBox.Text) 
                    ? null 
                    : SoundFilePathTextBox.Text;
                var soundService = new SoundService();
                soundService.PlayTestSound(soundFilePath);
                var testAlarm = new Alarm
                {
                    Title = string.IsNullOrWhiteSpace(TitleTextBox.Text) ? "테스트 알람" : TitleTextBox.Text,
                    SoundFilePath = soundFilePath,
                    AutoDismissMinutes = 0,
                    DateTime = DateTime.Now
                };
                var testPopup = new AlarmPopup(testAlarm, soundService: null, isTestMode: true);
                testPopup.Show();
                var timer = new System.Timers.Timer(3000);
                timer.Elapsed += (s, args) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            testPopup.Close();
                        }
                        catch { }
                    });
                };
                timer.AutoReset = false;
                timer.Enabled = true;
            }
            catch (Exception ex)
            {
                LogService.LogError("테스트 사운드 재생 중 오류", ex);
                MessageBox.Show($"테스트 사운드 재생 중 오류가 발생했습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void AutoDismissMinutesTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void QuickTimeTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                try
                {
                    var timeText = QuickTimeTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(timeText))
                        return;
                    if (timeText.Contains(':'))
                    {
                        var parts = timeText.Split(':');
                        if (parts.Length == 2 && 
                            int.TryParse(parts[0], out int hour) && 
                            int.TryParse(parts[1], out int minute))
                        {
                            if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60)
                            {
                                HourComboBox.SelectedItem = hour.ToString("00");
                                MinuteComboBox.SelectedItem = minute.ToString("00");
                                QuickTimeTextBox.Clear();
                                MessageBox.Show($"시간이 {hour:00}:{minute:00}로 설정되었습니다.", "시간 설정", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show("올바른 시간 범위를 입력하세요.\n(시간: 0-23, 분: 0-59)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            MessageBox.Show("시간 형식이 올바르지 않습니다.\n예: 15:30", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show("':' 기호를 포함하여 입력하세요.\n예: 15:30", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError("빠른 시간 입력 처리 중 오류", ex);
                    MessageBox.Show("시간 입력 처리 중 오류가 발생했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (DatePicker.SelectedDate == null)
            {
                MessageBox.Show("날짜를 선택해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var selectedItem = TypeComboBox.SelectedItem as ComboBoxItem;
            var typeTag = selectedItem?.Tag?.ToString();
            var isDday = typeTag == "Dday";
            var alarmType = isDday ? AlarmType.Dday : AlarmType.Alarm;

            var date = DatePicker.SelectedDate.Value;
            var categoryItem = CategoryComboBox.SelectedItem as ComboBoxItem;
            var category = categoryItem?.Tag?.ToString() ?? "기본";
            var priorityItem = PriorityComboBox.SelectedItem as ComboBoxItem;
            var priorityString = priorityItem?.Tag?.ToString() ?? "Normal";
            if (!Enum.TryParse<Priority>(priorityString, out var priority))
            {
                priority = Priority.Normal;
            }

            Alarm alarm;

            if (isDday)
            {
                var ddayRepeatType = RepeatComboBox.SelectedIndex == 1 ? RepeatType.Yearly : RepeatType.None;
                alarm = new Alarm
                {
                    Title = TitleTextBox.Text,
                    AlarmType = AlarmType.Dday,
                    TargetDate = date,
                    IsEnabled = true,
                    Category = category == "기본" ? null : category,
                    Priority = priority,
                    Memo = MemoTextBox?.Text?.Trim() ?? null,
                    DateTime = DateTime.Now,
                    RepeatType = ddayRepeatType,
                    SoundFilePath = null,
                    SelectedDaysOfWeek = new List<DayOfWeek>(),
                    AutoDismissMinutes = 1
                };
            }
            else
            {
                if (HourComboBox.SelectedItem == null || MinuteComboBox.SelectedItem == null)
                {
                    MessageBox.Show("시간을 선택해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var hour = int.Parse(HourComboBox.SelectedItem.ToString()!);
                var minute = int.Parse(MinuteComboBox.SelectedItem.ToString()!);
                var dateTime = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);

                var repeatType = (RepeatType)RepeatComboBox.SelectedIndex;
                var soundFilePath = string.IsNullOrWhiteSpace(SoundFilePathTextBox.Text) 
                    ? null 
                    : SoundFilePathTextBox.Text;
                if (!int.TryParse(AutoDismissMinutesTextBox.Text, out int autoDismissMinutes) || autoDismissMinutes < 0)
                {
                    MessageBox.Show("자동 종료 시간은 0분 이상이어야 합니다.\n(0분 = 자동 종료 안 함)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var selectedDays = new List<DayOfWeek>();
                if (repeatType == RepeatType.Weekly)
                {
                    if (MondayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Monday);
                    if (TuesdayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Tuesday);
                    if (WednesdayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Wednesday);
                    if (ThursdayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Thursday);
                    if (FridayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Friday);
                    if (SaturdayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Saturday);
                    if (SundayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Sunday);
                    if (selectedDays.Count == 0)
                    {
                        selectedDays.Add(dateTime.DayOfWeek);
                    }
                }

                alarm = new Alarm
                {
                    Title = TitleTextBox.Text,
                    AlarmType = AlarmType.Alarm,
                    DateTime = dateTime,
                    RepeatType = repeatType,
                    SoundFilePath = soundFilePath,
                    IsEnabled = true,
                    SelectedDaysOfWeek = selectedDays,
                    AutoDismissMinutes = autoDismissMinutes,
                    Category = category == "기본" ? null : category,
                    Priority = priority
                };
            }
            if (_existingAlarm != null)
            {
                alarm.Id = _existingAlarm.Id;
            }
            if (_existingAlarm == null)
            {
                var existingAlarms = StorageService.LoadAlarms();
                Alarm? duplicate = null;

                if (alarmType == AlarmType.Alarm)
                {
                    duplicate = existingAlarms.FirstOrDefault(a =>
                        a.AlarmType == AlarmType.Alarm &&
                        a.Title.Equals(alarm.Title, StringComparison.OrdinalIgnoreCase) &&
                        a.DateTime.Date == alarm.DateTime.Date &&
                        a.DateTime.Hour == alarm.DateTime.Hour &&
                        a.DateTime.Minute == alarm.DateTime.Minute);
                }
                else
                {
                    duplicate = existingAlarms.FirstOrDefault(a =>
                        a.AlarmType == AlarmType.Dday &&
                        a.Title.Equals(alarm.Title, StringComparison.OrdinalIgnoreCase) &&
                        a.TargetDate?.Date == alarm.TargetDate?.Date);
                }

                if (duplicate != null)
                {
                    var typeStr = alarmType == AlarmType.Alarm ? "알람" : "D-Day";
                    var dateStr = alarmType == AlarmType.Alarm
                        ? duplicate.DateTime.ToString("yyyy-MM-dd HH:mm")
                        : duplicate.TargetDate?.ToString("yyyy-MM-dd") ?? "";
                    MessageBox.Show(
                        $"동일한 {typeStr}이(가) 이미 존재합니다.\n\n제목: {duplicate.Title}\n날짜: {dateStr}",
                        $"중복 {typeStr}",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            Alarm = alarm;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DateDecrease_Click(object sender, RoutedEventArgs e)
        {
            if (DatePicker.SelectedDate.HasValue)
            {
                DatePicker.SelectedDate = DatePicker.SelectedDate.Value.AddDays(-1);
            }
            else
            {
                DatePicker.SelectedDate = DateTime.Today.AddDays(-1);
            }
        }

        private void DateToday_Click(object sender, RoutedEventArgs e)
        {
            DatePicker.SelectedDate = DateTime.Today;
        }

        private void DateTomorrow_Click(object sender, RoutedEventArgs e)
        {
            DatePicker.SelectedDate = DateTime.Today.AddDays(1);
        }

        private void DateIncrease_Click(object sender, RoutedEventArgs e)
        {
            if (DatePicker.SelectedDate.HasValue)
            {
                DatePicker.SelectedDate = DatePicker.SelectedDate.Value.AddDays(1);
            }
            else
            {
                DatePicker.SelectedDate = DateTime.Today.AddDays(1);
            }
        }
    }
}

