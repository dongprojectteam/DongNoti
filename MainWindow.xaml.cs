using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using DongNoti.Models;
using DongNoti.Services;
using DongNoti.Views;

namespace DongNoti
{
    public partial class MainWindow : Window
    {
        private List<Alarm> _alarms = new List<Alarm>();
        private CollectionViewSource? _alarmsViewSource;
        private CollectionViewSource? _ddaysViewSource;
        private bool _isSaving = false; // 저장 중 플래그
        private bool _isLoading = false; // 로드 중 플래그 (로드 중에는 SaveAlarms 방지)
        private bool _isInitialLoad = true; // 초기 로드 플래그 (앱 시작 시 첫 로드 완료까지 SaveAlarms 방지)

        public MainWindow()
        {
            InitializeComponent();
            // LoadAlarms는 App.xaml.cs에서 MainWindow 생성 후 호출하므로 여기서는 호출하지 않음
            
            // 집중모드 이벤트 구독
            FocusModeService.Instance.FocusModeChanged += OnFocusModeChanged;
            FocusModeService.Instance.FocusModeEnded += OnFocusModeEnded;
            
            // 초기 집중모드 상태 업데이트
            UpdateFocusModeUI();
            
            // 카테고리 필터 초기화
            LoadCategoryFilter();
            
            // Dday 창 토글 버튼 초기 상태 업데이트
            Loaded += (s, e) => UpdateDdayWindowToggleButton();
        }

        private void LoadCategoryFilter()
        {
            try
            {
                var settings = StorageService.LoadSettings();
                var categories = settings.AlarmCategories ?? AppSettings.GetDefaultAlarmCategories();

                // "기본"이 없으면 추가
                if (!categories.Contains("기본"))
                {
                    categories.Insert(0, "기본");
                }

                if (CategoryFilterComboBox != null)
                {
                    CategoryFilterComboBox.Items.Clear();
                    CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "전체", Tag = "All", IsSelected = true });
                    
                    foreach (var category in categories)
                    {
                        CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = category, Tag = category });
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("카테고리 필터 로드 중 오류", ex);
            }
        }

        public void LoadAlarms()
        {
            // 백그라운드 스레드에서 로드하여 UI 블로킹 방지
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // AlarmService에서 이미 로드된 알람 사용 (중복 파일 I/O 방지)
                    List<Alarm> loadedAlarms;
                    if (Application.Current is App app && app.AlarmService != null)
                    {
                        loadedAlarms = app.AlarmService.GetAlarms();
                    }
                    else
                    {
                        // AlarmService가 아직 초기화되지 않은 경우에만 파일에서 로드
                        loadedAlarms = StorageService.LoadAlarms();
                    }
                    
                    // UI 스레드에서 업데이트
                    Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            _isLoading = true; // 로드 시작 (가장 먼저 설정)
                            _alarms = loadedAlarms;
                            
                            // UI 강제 업데이트
                            var currentSelection = AlarmsDataGrid.SelectedItem;
                            
                            // ItemsSource를 null로 설정할 때도 이벤트가 발생할 수 있으므로 주의
                            AlarmsDataGrid.ItemsSource = null;
                            
                            // 약간의 지연을 두어 이벤트가 완전히 처리되도록 함
                            Dispatcher.BeginInvoke(() =>
                            {
                                try
                                {
                                    // 알람용 CollectionViewSource 설정 (Alarm 타입만, 지난 알람도 표시)
                                    var alarms = _alarms.Where(a => a.AlarmType == AlarmType.Alarm).ToList();
                                    _alarmsViewSource = new CollectionViewSource
                                    {
                                        Source = alarms
                                    };
                                    _alarmsViewSource.Filter += AlarmsViewSource_Filter;
                                    
                                    AlarmsDataGrid.ItemsSource = _alarmsViewSource.View;
                                    
                                    // Dday용 CollectionViewSource 설정 (Dday 타입만, 모든 Dday 표시)
                                    var ddays = _alarms.Where(a => a.AlarmType == AlarmType.Dday).ToList();
                                    _ddaysViewSource = new CollectionViewSource
                                    {
                                        Source = ddays
                                    };
                                    _ddaysViewSource.Filter += DdaysViewSource_Filter;
                                    
                                    DdaysDataGrid.ItemsSource = _ddaysViewSource.View;
                                    ApplyDefaultSort();
                                    
                                    // 선택 상태 복원
                                    if (currentSelection != null && currentSelection is Alarm selectedAlarm)
                                    {
                                        var restoredAlarm = _alarms.FirstOrDefault(a => a.Id == selectedAlarm.Id);
                                        if (restoredAlarm != null)
                                        {
                                            AlarmsDataGrid.SelectedItem = restoredAlarm;
                                        }
                                    }
                                    
                                    UpdateStatus();
                                    _isLoading = false; // 로드 완료 (모든 UI 업데이트 후)
                                    _isInitialLoad = false; // 초기 로드 완료 (이제부터 SaveAlarms 허용)
                                    // StorageService.LoadAlarms()에서 이미 로그를 출력하므로 중복 로그 제거
                                }
                                catch (Exception ex)
                                {
                                    _isLoading = false; // 오류 발생 시에도 플래그 해제
                                    _isInitialLoad = false; // 오류 발생 시에도 초기 로드 플래그 해제
                                    LogService.LogError("알람 로드 중 오류 (내부)", ex);
                                }
                            }, System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                        catch (Exception ex)
                        {
                            _isLoading = false; // 오류 발생 시에도 플래그 해제
                            _isInitialLoad = false; // 오류 발생 시에도 초기 로드 플래그 해제
                            LogService.LogError("알람 로드 중 오류", ex);
                            MessageBox.Show($"알람을 불러오는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogService.LogError("알람 로드 중 오류", ex);
                }
            });
        }

        private void SaveAlarms()
        {
            // 초기 로드 중에는 저장하지 않음 (앱 시작 시 불필요한 저장 방지)
            if (_isInitialLoad || _isLoading)
            {
                return;
            }
            
            // 중복 저장 방지
            if (_isSaving)
            {
                LogService.LogWarning("이미 저장 중입니다. 중복 저장 방지");
                return;
            }

            // 동기적으로 저장하여 즉시 반영 (파일 I/O는 빠르므로 UI 블로킹 최소화)
            try
            {
                if (_alarms == null)
                {
                    LogService.LogWarning("알람 목록이 null입니다. 빈 목록으로 초기화");
                    _alarms = new List<Alarm>();
                }

                _isSaving = true;
                StorageService.SaveAlarms(_alarms);
                
                // App의 AlarmService 새로고침 및 트레이 메뉴 업데이트
                if (Application.Current is App app)
                {
                    // RefreshAlarms() 내부에서 AlarmService.RefreshAlarms()와 트레이 메뉴 업데이트를 모두 수행
                    app.RefreshAlarms(refreshMainWindow: false);
                }
                
                UpdateStatus();
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 저장 중 오류", ex);
                MessageBox.Show($"알람을 저장하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void UpdateStatus()
        {
            try
            {
                if (_alarms == null)
                    return;

                // 빈 상태 표시 (지난 항목도 포함)
                var alarms = _alarms.Where(a => a.AlarmType == AlarmType.Alarm).ToList();
                var ddays = _alarms.Where(a => a.AlarmType == AlarmType.Dday).ToList();
                
                if (EmptyStatePanel != null && AlarmsDataGrid != null)
                {
                    EmptyStatePanel.Visibility = alarms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    AlarmsDataGrid.Visibility = alarms.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                }
                
                if (DdayEmptyStatePanel != null && DdaysDataGrid != null)
                {
                    DdayEmptyStatePanel.Visibility = ddays.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    DdaysDataGrid.Visibility = ddays.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                }
                    
                var enabledCount = _alarms.Count(a => a.IsEnabled);
                if (StatusText != null)
                {
                    StatusText.Text = $"총 {_alarms.Count}개 알람 (활성: {enabledCount}개)";
                }

                // 다음 알람 정보 업데이트
                UpdateNextAlarmInfo();
            }
            catch (Exception ex)
            {
                LogService.LogError("UpdateStatus 중 오류", ex);
            }
        }

        private void UpdateNextAlarmInfo()
        {
            try
            {
                if (NextAlarmText == null)
                    return;

                // 집중모드가 활성화되어 있으면 표시
                if (FocusModeService.Instance.IsFocusModeActive)
                {
                    var remaining = FocusModeService.Instance.GetRemainingTime();
                    NextAlarmText.Text = $"집중 모드 ({TimeHelper.FormatTimeSpan(remaining)} 남음)";
                    return;
                }

                var now = DateTime.Now;
                var enabledAlarms = _alarms.Where(a => a.IsEnabled).ToList();
                
                DateTime? nextAlarmTime = null;
                Alarm? nextAlarm = null;

                foreach (var alarm in enabledAlarms)
                {
                    var alarmTime = alarm.GetNextAlarmTime();
                    if (alarmTime.HasValue)
                    {
                        if (nextAlarmTime == null || alarmTime.Value < nextAlarmTime.Value)
                        {
                            nextAlarmTime = alarmTime.Value;
                            nextAlarm = alarm;
                        }
                    }
                }

                if (nextAlarmTime.HasValue && nextAlarm != null)
                {
                    var timeSpan = nextAlarmTime.Value - now;
                    string timeInfo;
                    
                    if (timeSpan.TotalDays >= 1)
                    {
                        timeInfo = $"{nextAlarmTime.Value:MM/dd HH:mm} ({(int)timeSpan.TotalDays}일 후)";
                    }
                    else if (timeSpan.TotalHours >= 1)
                    {
                        timeInfo = $"{nextAlarmTime.Value:HH:mm} ({(int)timeSpan.TotalHours}시간 후)";
                    }
                    else if (timeSpan.TotalMinutes >= 1)
                    {
                        timeInfo = $"{nextAlarmTime.Value:HH:mm} ({(int)timeSpan.TotalMinutes}분 후)";
                    }
                    else
                    {
                        timeInfo = $"{nextAlarmTime.Value:HH:mm} (곧)";
                    }

                    NextAlarmText.Text = $"{nextAlarm.Title} - {timeInfo}";
                }
                else
                {
                    NextAlarmText.Text = "없음";
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("UpdateNextAlarmInfo 중 오류", ex);
            }
        }

        /// <summary>
        /// 알람 목록을 새로고침합니다 (ItemsSource를 null로 설정하지 않고 직접 업데이트)
        /// </summary>
        public void RefreshAlarmsList()
        {
            try
            {
                if (Application.Current is App app && app.AlarmService != null)
                {
                    var updatedAlarms = app.AlarmService.GetAlarms();
                    
                    // 현재 선택된 항목 저장
                    var currentAlarmSelection = AlarmsDataGrid.SelectedItem as Alarm;
                    var currentDdaySelection = DdaysDataGrid.SelectedItem as Alarm;
                    
                    // _alarms 업데이트
                    _alarms = updatedAlarms;
                    
                    // 알람용 CollectionViewSource 새로 생성 (변환기 재호출을 위해, 지난 알람도 표시)
                    var alarms = _alarms.Where(a => a.AlarmType == AlarmType.Alarm).ToList();
                    if (_alarmsViewSource != null)
                    {
                        _alarmsViewSource.Filter -= AlarmsViewSource_Filter;
                    }
                    _alarmsViewSource = new CollectionViewSource { Source = alarms };
                    _alarmsViewSource.Filter += AlarmsViewSource_Filter;
                    AlarmsDataGrid.ItemsSource = null;
                    AlarmsDataGrid.ItemsSource = _alarmsViewSource.View;
                    // 행 스타일의 변환기 재호출을 위해 Items.Refresh 호출
                    AlarmsDataGrid.Items.Refresh();
                    
                    // Dday용 CollectionViewSource 새로 생성 (변환기 재호출을 위해, 모든 Dday 표시)
                    var ddays = _alarms.Where(a => a.AlarmType == AlarmType.Dday).ToList();
                    if (_ddaysViewSource != null)
                    {
                        _ddaysViewSource.Filter -= DdaysViewSource_Filter;
                    }
                    _ddaysViewSource = new CollectionViewSource { Source = ddays };
                    _ddaysViewSource.Filter += DdaysViewSource_Filter;
                    DdaysDataGrid.ItemsSource = null;
                    DdaysDataGrid.ItemsSource = _ddaysViewSource.View;
                    // 행 스타일의 변환기 재호출을 위해 Items.Refresh 호출
                    DdaysDataGrid.Items.Refresh();
                    
                    ApplyDefaultSort();
                    
                    // 선택 상태 복원
                    if (currentAlarmSelection != null)
                    {
                        var restoredAlarm = updatedAlarms.FirstOrDefault(a => a.Id == currentAlarmSelection.Id);
                        if (restoredAlarm != null && restoredAlarm.AlarmType == AlarmType.Alarm)
                        {
                            AlarmsDataGrid.SelectedItem = restoredAlarm;
                        }
                    }
                    
                    if (currentDdaySelection != null)
                    {
                        var restoredDday = updatedAlarms.FirstOrDefault(a => a.Id == currentDdaySelection.Id);
                        if (restoredDday != null && restoredDday.AlarmType == AlarmType.Dday && !restoredDday.IsDdayPassed)
                        {
                            DdaysDataGrid.SelectedItem = restoredDday;
                        }
                    }
                    
                    // 상태 업데이트
                    UpdateStatus();
                    
                    LogService.LogDebug("알람 목록 새로고침 완료 (ItemsSource 직접 업데이트)");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 목록 새로고침 중 오류", ex);
            }
        }

        private void AlarmsViewSource_Filter(object sender, FilterEventArgs e)
        {
            try
            {
                if (e.Item is not Alarm alarm)
                {
                    e.Accepted = false;
                    return;
                }

                // Alarm 타입만 필터링 (이미 Source에서 필터링했지만 안전을 위해)
                if (alarm.AlarmType != AlarmType.Alarm)
                {
                    e.Accepted = false;
                    return;
                }

                // 지난 알람은 필터링하지 않음 (표시하되 스타일로 구분)

                // 검색 필터
                if (SearchTextBox != null)
                {
                    var searchText = SearchTextBox.Text?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        if (!alarm.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            e.Accepted = false;
                            return;
                        }
                    }
                }

                // 필터 ComboBox
                if (FilterComboBox != null)
                {
                    var filterItem = FilterComboBox.SelectedItem as ComboBoxItem;
                    var filterTag = filterItem?.Tag?.ToString() ?? "All";

                    switch (filterTag)
                    {
                        case "DdayOnly":
                            // Dday는 별도 DataGrid에 표시되므로 여기서는 제외
                            e.Accepted = false;
                            return;
                        case "Enabled":
                            if (!alarm.IsEnabled)
                            {
                                e.Accepted = false;
                                return;
                            }
                            break;
                        case "Disabled":
                            if (alarm.IsEnabled)
                            {
                                e.Accepted = false;
                                return;
                            }
                            break;
                        case "None":
                            if (alarm.RepeatType != RepeatType.None)
                            {
                                e.Accepted = false;
                                return;
                            }
                            break;
                        case "Daily":
                            if (alarm.RepeatType != RepeatType.Daily)
                            {
                                e.Accepted = false;
                                return;
                            }
                            break;
                        case "Weekly":
                            if (alarm.RepeatType != RepeatType.Weekly)
                            {
                                e.Accepted = false;
                                return;
                            }
                            break;
                        case "Monthly":
                            if (alarm.RepeatType != RepeatType.Monthly)
                            {
                                e.Accepted = false;
                                return;
                            }
                            break;
                    }
                }

                // 카테고리 필터
                if (CategoryFilterComboBox != null)
                {
                    var categoryFilterItem = CategoryFilterComboBox.SelectedItem as ComboBoxItem;
                    var categoryFilterTag = categoryFilterItem?.Tag?.ToString() ?? "All";
                    
                    if (categoryFilterTag != "All")
                    {
                        var alarmCategory = alarm.Category ?? "기본";
                        if (alarmCategory != categoryFilterTag)
                        {
                            e.Accepted = false;
                            return;
                        }
                    }
                }

                e.Accepted = true;
            }
            catch (Exception ex)
            {
                LogService.LogError("필터 적용 중 오류", ex);
                e.Accepted = true; // 오류 발생 시 모든 항목 표시
            }
        }

        private void DdaysViewSource_Filter(object sender, FilterEventArgs e)
        {
            try
            {
                if (e.Item is not Alarm dday)
                {
                    e.Accepted = false;
                    return;
                }

                // Dday 타입만 필터링 (이미 Source에서 필터링했지만 안전을 위해, 모든 Dday 표시)
                if (dday.AlarmType != AlarmType.Dday)
                {
                    e.Accepted = false;
                    return;
                }

                // 검색 필터
                if (SearchTextBox != null)
                {
                    var searchText = SearchTextBox.Text?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        if (!dday.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            e.Accepted = false;
                            return;
                        }
                    }
                }

                // 필터 ComboBox (DdayOnly만 허용)
                if (FilterComboBox != null)
                {
                    var filterItem = FilterComboBox.SelectedItem as ComboBoxItem;
                    var filterTag = filterItem?.Tag?.ToString() ?? "All";

                    if (filterTag == "AlarmOnly")
                    {
                        // 알람만 필터는 Dday를 제외
                        e.Accepted = false;
                        return;
                    }
                }

                // 카테고리 필터
                if (CategoryFilterComboBox != null)
                {
                    var categoryFilterItem = CategoryFilterComboBox.SelectedItem as ComboBoxItem;
                    var categoryFilterTag = categoryFilterItem?.Tag?.ToString() ?? "All";
                    
                    if (categoryFilterTag != "All")
                    {
                        var ddayCategory = dday.Category ?? "기본";
                        if (ddayCategory != categoryFilterTag)
                        {
                            e.Accepted = false;
                            return;
                        }
                    }
                }

                e.Accepted = true;
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 필터 적용 중 오류", ex);
                e.Accepted = true; // 오류 발생 시 모든 항목 표시
            }
        }

        private void ApplyDefaultSort()
        {
            try
            {
                if (_alarmsViewSource?.View == null) return;

                using (_alarmsViewSource.View.DeferRefresh())
                {
                    _alarmsViewSource.View.SortDescriptions.Clear();
                    _alarmsViewSource.View.SortDescriptions.Add(
                        new SortDescription(nameof(Alarm.PrioritySortKey), ListSortDirection.Descending));
                    _alarmsViewSource.View.SortDescriptions.Add(
                        new SortDescription(nameof(Alarm.DateTime), ListSortDirection.Ascending));
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("기본 정렬 적용 중 오류", ex);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchTextBox != null)
            {
                // 검색창이 비어있지 않으면 X 버튼 표시
                ClearSearchButton.Visibility = string.IsNullOrWhiteSpace(SearchTextBox.Text) 
                    ? Visibility.Collapsed 
                    : Visibility.Visible;
            }

            // 필터 새로고침
            _alarmsViewSource?.View?.Refresh();
            _ddaysViewSource?.View?.Refresh();
            UpdateStatus();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox != null)
            {
                SearchTextBox.Text = string.Empty;
                SearchTextBox.Focus();
            }
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // 필터 새로고침 - 즉시 반영
                if (_alarmsViewSource != null && _alarmsViewSource.View != null)
                {
                    _alarmsViewSource.View.Refresh();
                }
                if (_ddaysViewSource != null && _ddaysViewSource.View != null)
                {
                    _ddaysViewSource.View.Refresh();
                }
                UpdateStatus();
            }
            catch (Exception ex)
            {
                LogService.LogError("필터 변경 중 오류", ex);
            }
        }

        private void CategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // 필터 새로고침 - 즉시 반영
                if (_alarmsViewSource != null && _alarmsViewSource.View != null)
                {
                    _alarmsViewSource.View.Refresh();
                }
                if (_ddaysViewSource != null && _ddaysViewSource.View != null)
                {
                    _ddaysViewSource.View.Refresh();
                }
                UpdateStatus();
            }
            catch (Exception ex)
            {
                LogService.LogError("카테고리 필터 변경 중 오류", ex);
            }
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Alarm? selected = null;
                if (AlarmsDataGrid.SelectedItem is Alarm alarm)
                    selected = alarm;
                else if (DdaysDataGrid?.SelectedItem is Alarm dday)
                    selected = dday;

                if (selected == null)
                {
                    MessageBox.Show("데이터 그리드에서 복제할 알람 또는 Dday를 선택해주세요.",
                        "선택 없음",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (_alarms == null)
                    _alarms = new List<Alarm>();

                // 복제용 알람 생성 (새 Id, 나머지는 동일)
                var clone = new Alarm
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = selected.Title,
                    DateTime = selected.DateTime,
                    RepeatType = selected.RepeatType,
                    IsEnabled = true,
                    SoundFilePath = selected.SoundFilePath,
                    LastTriggered = null,
                    SelectedDaysOfWeek = selected.SelectedDaysOfWeek != null ? new List<DayOfWeek>(selected.SelectedDaysOfWeek) : new List<DayOfWeek>(),
                    IsTemporary = selected.IsTemporary,
                    AutoDismissMinutes = selected.AutoDismissMinutes,
                    Category = selected.Category,
                    Priority = selected.Priority,
                    AlarmType = selected.AlarmType,
                    TargetDate = selected.TargetDate,
                    Memo = selected.Memo
                };

                var dialog = new AlarmDialog(clone, forClone: true);
                if (dialog.ShowDialog() == true && dialog.Alarm != null)
                {
                    _alarms.Add(dialog.Alarm);
                    SaveAlarms();
                    RefreshAlarmsList();
                    if (Application.Current is App app)
                    {
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                            ddayWindow.RefreshDdayList();
                    }
                    var itemType = dialog.Alarm.AlarmType == AlarmType.Dday ? "Dday" : "알람";
                    LogService.LogInfo($"{itemType} 복제 완료: '{dialog.Alarm.Title}'");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("항목 복제 중 오류", ex);
                MessageBox.Show($"항목 복제 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AddAlarm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_alarms == null)
                {
                    _alarms = new List<Alarm>();
                }

                var dialog = new AlarmDialog();
                if (dialog.ShowDialog() == true && dialog.Alarm != null)
                {
                    _alarms.Add(dialog.Alarm);
                    SaveAlarms(); // 동기적으로 저장 (AlarmService도 자동으로 새로고침됨)
                    
                    // UI 업데이트 (AlarmService에서 최신 데이터 가져옴)
                    RefreshAlarmsList();
                    
                    // DdayWindow도 즉시 새로고침
                    if (Application.Current is App app)
                    {
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                        {
                            ddayWindow.RefreshDdayList();
                        }
                    }
                    
                    LogService.LogInfo($"항목 추가 완료: '{dialog.Alarm.Title}'");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("항목 추가 중 오류", ex);
                var errorMessage = $"항목 추가 중 오류가 발생했습니다: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n내부 오류: {ex.InnerException.Message}";
                }
                MessageBox.Show(errorMessage, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditAlarm_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedAlarm();
        }

        private void DeleteAlarm_Click(object sender, RoutedEventArgs e)
        {
            // 알람 또는 Dday 삭제
            Alarm? selectedAlarm = null;
            bool isDday = false;
            
            if (AlarmsDataGrid.SelectedItem is Alarm alarm)
            {
                selectedAlarm = alarm;
            }
            else if (DdaysDataGrid?.SelectedItem is Alarm dday)
            {
                selectedAlarm = dday;
                isDday = true;
            }

            if (selectedAlarm != null)
            {
                var itemType = isDday ? "Dday" : "알람";
                var result = MessageBox.Show(
                    $"'{selectedAlarm.Title}' {itemType}을(를) 삭제하시겠습니까?",
                    $"{itemType} 삭제",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // ID로 찾아서 삭제
                    var index = _alarms.FindIndex(a => a.Id == selectedAlarm.Id);
                    if (index >= 0)
                    {
                        _alarms.RemoveAt(index);
                    }
                    SaveAlarms(); // 동기적으로 저장 (AlarmService도 자동으로 새로고침됨)
                    
                    // UI 업데이트 (AlarmService에서 최신 데이터 가져옴)
                    RefreshAlarmsList();
                    
                    LogService.LogInfo($"{itemType} 삭제 완료: '{selectedAlarm.Title}'");
                }
            }
        }

        private void AlarmsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                e.Handled = true; // 이벤트 처리 완료 표시
                
                // 더블클릭한 위치의 행 찾기
                var dataGrid = sender as DataGrid;
                if (dataGrid == null) return;

                var dependencyObject = (System.Windows.DependencyObject)e.OriginalSource;
                while (dependencyObject != null && !(dependencyObject is DataGridRow))
                {
                    dependencyObject = System.Windows.Media.VisualTreeHelper.GetParent(dependencyObject);
                }

                if (dependencyObject is DataGridRow row)
                {
                    dataGrid.SelectedItem = row.Item;
                    EditSelectedAlarm();
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("더블클릭 이벤트 처리 중 오류", ex);
                MessageBox.Show($"오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditSelectedAlarm()
        {
            try
            {
                Alarm? selectedAlarm = null;
                
                if (AlarmsDataGrid.SelectedItem is Alarm alarm)
                {
                    selectedAlarm = alarm;
                }
                else if (DdaysDataGrid?.SelectedItem is Alarm dday)
                {
                    selectedAlarm = dday;
                }
                
                if (selectedAlarm != null)
                {
                    var dialog = new AlarmDialog(selectedAlarm);
                    if (dialog.ShowDialog() == true && dialog.Alarm != null)
                    {
                        // ID로 찾기 (View에서 가져온 항목과 리스트의 항목이 다른 참조일 수 있음)
                        var index = _alarms.FindIndex(a => a.Id == selectedAlarm.Id);
                        if (index >= 0)
                        {
                            var updatedAlarm = dialog.Alarm;
                            
                            // 알람 타입에 따라 처리
                            if (updatedAlarm.AlarmType == AlarmType.Alarm)
                            {
                                // 알람 시간에 따라 LastTriggered 업데이트
                                var now = DateTime.Now;
                                var alarmMinute = new DateTime(updatedAlarm.DateTime.Year, updatedAlarm.DateTime.Month, updatedAlarm.DateTime.Day, 
                                                              updatedAlarm.DateTime.Hour, updatedAlarm.DateTime.Minute, 0);
                                var nowMinute = TimeHelper.ToMinutePrecision(now);
                                
                                // 반복 없는 알람만 처리
                                if (updatedAlarm.RepeatType == RepeatType.None)
                                {
                                    if (alarmMinute >= nowMinute)
                                    {
                                        // 미래 시간으로 수정 → LastTriggered 리셋
                                        if (updatedAlarm.LastTriggered != null)
                                        {
                                            updatedAlarm.LastTriggered = null;
                                            LogService.LogInfo($"알람 '{updatedAlarm.Title}' 시간이 미래로 수정됨. LastTriggered 리셋");
                                        }
                                    }
                                    else
                                    {
                                        // 과거 시간으로 수정 → LastTriggered 설정 (이미 울린 것으로 표시)
                                        if (updatedAlarm.LastTriggered == null)
                                        {
                                            updatedAlarm.LastTriggered = alarmMinute;
                                            LogService.LogInfo($"알람 '{updatedAlarm.Title}' 시간이 과거로 수정됨. LastTriggered 설정");
                                        }
                                    }
                                }
                            }
                            // Dday 타입은 LastTriggered 업데이트 불필요 (메모만 업데이트)
                            
                            _alarms[index] = updatedAlarm;
                            SaveAlarms(); // 동기적으로 저장 (AlarmService도 자동으로 새로고침됨)
                            
                            // UI 업데이트 (AlarmService에서 최신 데이터 가져옴)
                            RefreshAlarmsList();
                            
                            // DdayWindow도 즉시 새로고침
                            if (Application.Current is App app)
                            {
                                var ddayWindow = app.GetDdayWindow();
                                if (ddayWindow != null && ddayWindow.IsVisible)
                                {
                                    ddayWindow.RefreshDdayList();
                                }
                            }
                            
                            var itemType = updatedAlarm.AlarmType == AlarmType.Dday ? "Dday" : "알람";
                            LogService.LogInfo($"{itemType} 수정 완료: '{selectedAlarm.Title}'");
                        }
                    }
                }
                else
                {
                    LogService.LogWarning("수정할 알람이 선택되지 않음");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 수정 중 오류", ex);
                MessageBox.Show($"알람 수정 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AlarmToggle_Checked(object sender, RoutedEventArgs e)
        {
            // 초기 로드 중에는 저장하지 않음
            if (_isInitialLoad || _isLoading)
                return;
                
            if (sender is CheckBox checkBox && checkBox.DataContext is Alarm alarm)
            {
                alarm.IsEnabled = true;
                SaveAlarms();
            }
        }

        private void AlarmToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            // 초기 로드 중에는 저장하지 않음
            if (_isInitialLoad || _isLoading)
                return;
                
            if (sender is CheckBox checkBox && checkBox.DataContext is Alarm alarm)
            {
                alarm.IsEnabled = false;
                SaveAlarms();
            }
        }

        private void DdaysDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Dday 선택 시 Edit/Delete 버튼 활성화
            if (DdaysDataGrid.SelectedItem is Alarm)
            {
                EditButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
                // 알람 DataGrid 선택 해제
                if (AlarmsDataGrid != null)
                {
                    AlarmsDataGrid.SelectedItem = null;
                }
            }
            else
            {
                // 알람 DataGrid에서도 선택이 없으면 버튼 비활성화
                if (AlarmsDataGrid.SelectedItem == null)
                {
                    EditButton.IsEnabled = false;
                    DeleteButton.IsEnabled = false;
                }
            }
        }

        private void DdaysDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                e.Handled = true;
                
                var dataGrid = sender as DataGrid;
                if (dataGrid == null) return;

                var dependencyObject = (System.Windows.DependencyObject)e.OriginalSource;
                while (dependencyObject != null && !(dependencyObject is DataGridRow))
                {
                    dependencyObject = System.Windows.Media.VisualTreeHelper.GetParent(dependencyObject);
                }

                if (dependencyObject is DataGridRow row)
                {
                    dataGrid.SelectedItem = row.Item;
                    EditSelectedAlarm();
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 더블클릭 이벤트 처리 중 오류", ex);
            }
        }


        private void DdayToggle_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox checkBox && checkBox.DataContext is Alarm dday)
                {
                    dday.IsEnabled = true;
                    SaveAlarms();
                    LogService.LogInfo($"Dday 활성화: '{dday.Title}'");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 활성화 중 오류", ex);
            }
        }

        private void DdayToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox checkBox && checkBox.DataContext is Alarm dday)
                {
                    dday.IsEnabled = false;
                    SaveAlarms();
                    LogService.LogInfo($"Dday 비활성화: '{dday.Title}'");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 비활성화 중 오류", ex);
            }
        }

        private void AlarmsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 알람 선택 시 Edit/Delete 버튼 활성화
            if (AlarmsDataGrid.SelectedItem is Alarm)
            {
                EditButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
                // Dday 선택 해제
                if (DdaysDataGrid != null)
                {
                    DdaysDataGrid.SelectedItem = null;
                }
            }
            else
            {
                // Dday에서도 선택이 없으면 버튼 비활성화
                if (DdaysDataGrid?.SelectedItem == null)
                {
                    EditButton.IsEnabled = false;
                    DeleteButton.IsEnabled = false;
                }
            }
        }

        private void AlarmsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                // 편집이 커밋될 때만 저장
                if (e.EditAction == DataGridEditAction.Commit)
                {
                    // UI 업데이트를 위해 Dispatcher 사용 (낮은 우선순위로 변경)
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // 편집된 알람의 시간에 따라 LastTriggered 업데이트
                            if (e.Row.Item is Alarm editedAlarm)
                            {
                                var now = DateTime.Now;
                                var alarmMinute = new DateTime(editedAlarm.DateTime.Year, editedAlarm.DateTime.Month, editedAlarm.DateTime.Day, 
                                                              editedAlarm.DateTime.Hour, editedAlarm.DateTime.Minute, 0);
                                var nowMinute = TimeHelper.ToMinutePrecision(now);
                                
                                // 반복 없는 알람만 처리
                                if (editedAlarm.RepeatType == RepeatType.None)
                                {
                                    if (alarmMinute >= nowMinute)
                                    {
                                        // 미래 시간으로 수정 → LastTriggered 리셋
                                        if (editedAlarm.LastTriggered != null)
                                        {
                                            editedAlarm.LastTriggered = null;
                                            LogService.LogInfo($"알람 '{editedAlarm.Title}' 시간이 미래로 수정됨. LastTriggered 리셋");
                                        }
                                    }
                                    else
                                    {
                                        // 과거 시간으로 수정 → LastTriggered 설정 (이미 울린 것으로 표시)
                                        if (editedAlarm.LastTriggered == null)
                                        {
                                            editedAlarm.LastTriggered = alarmMinute;
                                            LogService.LogInfo($"알람 '{editedAlarm.Title}' 시간이 과거로 수정됨. LastTriggered 설정");
                                        }
                                    }
                                }
                            }

                            // ItemsSource 재설정 없이 저장만 수행
                            if (_alarms == null)
                            {
                                LogService.LogWarning("알람 목록이 null입니다. 빈 목록으로 초기화");
                                _alarms = new List<Alarm>();
                            }

                            if (!_isSaving)
                            {
                                _isSaving = true;
                                StorageService.SaveAlarms(_alarms);
                                UpdateStatus();
                                
                                // AlarmService 새로고침 (백그라운드로)
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (Application.Current is App app)
                                    {
                                        app.RefreshAlarms(refreshMainWindow: false);
                                    }
                                }), System.Windows.Threading.DispatcherPriority.Background);
                                
                                _isSaving = false;
                                LogService.LogInfo($"DataGrid 편집 후 알람 저장 완료");
                                
                                // UI 새로고침하여 색상 업데이트 (ItemsSource 재설정)
                                var currentSelection = AlarmsDataGrid.SelectedItem;
                                AlarmsDataGrid.ItemsSource = null;
                                AlarmsDataGrid.ItemsSource = _alarms;
                                if (currentSelection != null)
                                {
                                    AlarmsDataGrid.SelectedItem = currentSelection;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _isSaving = false;
                            LogService.LogError("DataGrid 편집 후 저장 중 오류", ex);
                        }
                    }), System.Windows.Threading.DispatcherPriority.ContextIdle);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("CellEditEnding 이벤트 처리 중 오류", ex);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.ShowDialog();
            if (Application.Current is App app)
            {
                app.RefreshAlarms(refreshMainWindow: true);
            }
        }

        private void FocusMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FocusModeService.Instance.IsFocusModeActive)
                {
                    // 이미 활성화되어 있으면 종료
                    var result = MessageBox.Show(
                        $"집중 모드를 종료하시겠습니까?\n\n남은 시간: {TimeHelper.FormatTimeSpan(FocusModeService.Instance.GetRemainingTime())}",
                        "집중 모드",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        FocusModeService.Instance.StopFocusMode();
                    }
                }
                else
                {
                    // 다이얼로그로 시간 선택
                    var dialog = new FocusModeDialog();
                    dialog.Owner = this;
                    
                    if (dialog.ShowDialog() == true)
                    {
                        FocusModeService.Instance.StartFocusMode(dialog.SelectedMinutes);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("집중모드 버튼 클릭 처리 중 오류", ex);
                MessageBox.Show("집중 모드 처리 중 오류가 발생했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnFocusModeChanged(bool isActive)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateFocusModeUI();
                UpdateStatus();
            });
        }

        private void OnFocusModeEnded()
        {
            Dispatcher.BeginInvoke(() =>
            {
                var missedAlarms = FocusModeService.Instance.GetMissedAlarms();
                if (missedAlarms.Count > 0)
                {
                    var summaryWindow = new MissedAlarmsSummaryWindow();
                    summaryWindow.ShowDialog();
                }
            });
        }

        private void UpdateFocusModeUI()
        {
            try
            {
                if (FocusModeButton != null)
                {
                    if (FocusModeService.Instance.IsFocusModeActive)
                    {
                        FocusModeButton.Content = "🌙 집중 모드 (활성)";
                        FocusModeButton.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00)); // 오렌지색
                    }
                    else
                    {
                        FocusModeButton.Content = "🌙 집중 모드";
                        FocusModeButton.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3)); // 파란색 (기본)
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("집중모드 UI 업데이트 중 오류", ex);
            }
        }

        private void Statistics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var statisticsWindow = new StatisticsWindow();
                statisticsWindow.Owner = this;
                statisticsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogService.LogError("통계 창 열기 중 오류", ex);
                MessageBox.Show($"통계 창을 열 수 없습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void DdayWindowToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Application.Current is App app)
                {
                    app.ShowDdayWindow();
                    UpdateDdayWindowToggleButton();
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 창 토글 중 오류", ex);
            }
        }

        public void UpdateDdayWindowToggleButton()
        {
            try
            {
                if (Application.Current is App app && DdayWindowToggleButton != null)
                {
                    // App에서 DdayWindow 상태 확인
                    var ddayWindow = app.GetDdayWindow();
                    bool isVisible = ddayWindow != null && ddayWindow.IsVisible;
                    DdayWindowToggleButton.Content = isVisible ? "Dday 창 끄기" : "Dday 창";
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 창 토글 버튼 업데이트 중 오류", ex);
            }
        }

        private void DataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                // Delete: 선택된 알람 또는 Dday 삭제
                if (e.Key == System.Windows.Input.Key.Delete)
                {
                    // 셀 편집 중인지 확인 - 편집 중이면 Delete 키를 셀 내 텍스트 삭제로 전달
                    if (IsCellEditing(e.OriginalSource as DependencyObject))
                    {
                        return; // 편집 중이면 행 삭제 처리하지 않음 (텍스트 삭제로 동작)
                    }

                    // AlarmsDataGrid 또는 DdaysDataGrid 중 하나라도 선택되어 있고 Delete 버튼이 활성화되어 있으면 삭제
                    if ((AlarmsDataGrid.SelectedItem != null || (DdaysDataGrid?.SelectedItem != null)) && DeleteButton.IsEnabled)
                    {
                        DeleteAlarm_Click(sender, e);
                        e.Handled = true; // DataGrid의 기본 삭제 동작 방지
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("DataGrid 키 입력 처리 중 오류", ex);
            }
        }

        /// <summary>
        /// DataGrid 셀이 편집 중인지 확인합니다.
        /// </summary>
        private bool IsCellEditing(DependencyObject? element)
        {
            if (element == null)
                return false;

            // VisualTree를 타고 올라가며 DataGridCell을 찾음
            DependencyObject? current = element;
            while (current != null)
            {
                if (current is DataGridCell cell)
                {
                    return cell.IsEditing;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                // Ctrl+N: 새 항목 추가
                if (e.Key == System.Windows.Input.Key.N && 
                    (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    AddAlarm_Click(sender, e);
                    e.Handled = true;
                    return;
                }

                // Ctrl+F: 검색창 포커스
                if (e.Key == System.Windows.Input.Key.F && 
                    (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    SearchTextBox?.Focus();
                    e.Handled = true;
                    return;
                }

                // Enter 또는 F2: 선택된 알람 편집
                if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.F2)
                {
                    if (AlarmsDataGrid.SelectedItem != null && EditButton.IsEnabled)
                    {
                        EditAlarm_Click(sender, e);
                        e.Handled = true;
                        return;
                    }
                }

                // Escape: 선택 해제
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    AlarmsDataGrid.SelectedItem = null;
                    e.Handled = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("키보드 단축키 처리 중 오류", ex);
            }
        }

        private void OrganizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.ContextMenu != null)
                {
                    // 개수 계산
                    var pastAlarms = _alarms.Where(a => 
                        a.AlarmType == AlarmType.Alarm && 
                        a.RepeatType == RepeatType.None && 
                        a.GetNextAlarmTime() == null).ToList();
                    
                    var pastDdays = _alarms.Where(a => 
                        a.AlarmType == AlarmType.Dday && 
                        a.IsDdayPassed).ToList();
                    
                    var allAlarms = _alarms.Where(a => a.AlarmType == AlarmType.Alarm).ToList();
                    var allDdays = _alarms.Where(a => a.AlarmType == AlarmType.Dday).ToList();
                    
                    var disabledAlarms = allAlarms.Where(a => !a.IsEnabled).ToList();
                    var disabledDdays = allDdays.Where(a => !a.IsEnabled).ToList();
                    
                    var enabledAlarms = allAlarms.Where(a => a.IsEnabled).ToList();
                    var enabledDdays = allDdays.Where(a => a.IsEnabled).ToList();
                    
                    // 메뉴 항목 업데이트
                    if (button.ContextMenu.Items.Count > 0)
                    {
                        // 지난 알람 삭제
                        if (button.ContextMenu.Items[0] is MenuItem pastDeleteMenu && pastDeleteMenu.Items.Count >= 3)
                        {
                            ((MenuItem)pastDeleteMenu.Items[0]).Header = $"알람 삭제 ({pastAlarms.Count}개)";
                            ((MenuItem)pastDeleteMenu.Items[1]).Header = $"Dday 삭제 ({pastDdays.Count}개)";
                            ((MenuItem)pastDeleteMenu.Items[2]).Header = $"알람 및 Dday 모두 삭제 ({pastAlarms.Count + pastDdays.Count}개)";
                        }
                        
                        // 모두 삭제 (Separator 다음)
                        if (button.ContextMenu.Items.Count > 2 && button.ContextMenu.Items[2] is MenuItem deleteAllMenu && deleteAllMenu.Items.Count >= 3)
                        {
                            ((MenuItem)deleteAllMenu.Items[0]).Header = $"알람 삭제 ({allAlarms.Count}개)";
                            ((MenuItem)deleteAllMenu.Items[1]).Header = $"Dday 삭제 ({allDdays.Count}개)";
                            ((MenuItem)deleteAllMenu.Items[2]).Header = $"알람 및 Dday 모두 삭제 ({allAlarms.Count + allDdays.Count}개)";
                        }
                        
                        // 모두 활성화 (Separator 다음)
                        if (button.ContextMenu.Items.Count > 4 && button.ContextMenu.Items[4] is MenuItem enableAllMenu && enableAllMenu.Items.Count >= 3)
                        {
                            ((MenuItem)enableAllMenu.Items[0]).Header = $"알람 활성화 ({disabledAlarms.Count}개)";
                            ((MenuItem)enableAllMenu.Items[1]).Header = $"Dday 활성화 ({disabledDdays.Count}개)";
                            ((MenuItem)enableAllMenu.Items[2]).Header = $"알람 및 Dday 모두 활성화 ({disabledAlarms.Count + disabledDdays.Count}개)";
                        }
                        
                        // 모두 비활성화 (Separator 다음)
                        if (button.ContextMenu.Items.Count > 6 && button.ContextMenu.Items[6] is MenuItem disableAllMenu && disableAllMenu.Items.Count >= 3)
                        {
                            ((MenuItem)disableAllMenu.Items[0]).Header = $"알람 비활성화 ({enabledAlarms.Count}개)";
                            ((MenuItem)disableAllMenu.Items[1]).Header = $"Dday 비활성화 ({enabledDdays.Count}개)";
                            ((MenuItem)disableAllMenu.Items[2]).Header = $"알람 및 Dday 모두 비활성화 ({enabledAlarms.Count + enabledDdays.Count}개)";
                        }
                    }
                    
                    button.ContextMenu.PlacementTarget = button;
                    button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    button.ContextMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("정리 버튼 클릭 처리 중 오류", ex);
            }
        }

        private void DeletePastAlarms_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pastAlarms = _alarms.Where(a => 
                    a.AlarmType == AlarmType.Alarm && 
                    a.RepeatType == RepeatType.None && 
                    a.GetNextAlarmTime() == null).ToList();

                if (pastAlarms.Count == 0)
                {
                    MessageBox.Show("삭제할 지난 알람이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"지난 알람 {pastAlarms.Count}개를 삭제하시겠습니까?",
                    "지난 알람 삭제",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var alarm in pastAlarms)
                    {
                        _alarms.Remove(alarm);
                    }
                    SaveAlarms();
                    RefreshAlarmsList();
                    LogService.LogInfo($"지난 알람 {pastAlarms.Count}개 삭제 완료");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("지난 알람 삭제 중 오류", ex);
                MessageBox.Show($"지난 알람 삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeletePastDdays_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pastDdays = _alarms.Where(a => 
                    a.AlarmType == AlarmType.Dday && 
                    a.IsDdayPassed).ToList();

                if (pastDdays.Count == 0)
                {
                    MessageBox.Show("삭제할 지난 Dday가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"지난 Dday {pastDdays.Count}개를 삭제하시겠습니까?",
                    "지난 Dday 삭제",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var dday in pastDdays)
                    {
                        _alarms.Remove(dday);
                    }
                    SaveAlarms();
                    RefreshAlarmsList();
                    
                    // DdayWindow도 새로고침
                    if (Application.Current is App app)
                    {
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                        {
                            ddayWindow.RefreshDdayList();
                        }
                    }
                    
                    LogService.LogInfo($"지난 Dday {pastDdays.Count}개 삭제 완료");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("지난 Dday 삭제 중 오류", ex);
                MessageBox.Show($"지난 Dday 삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeletePastAlarmsAndDdays_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pastAlarms = _alarms.Where(a => 
                    a.AlarmType == AlarmType.Alarm && 
                    a.RepeatType == RepeatType.None && 
                    a.GetNextAlarmTime() == null).ToList();
                
                var pastDdays = _alarms.Where(a => 
                    a.AlarmType == AlarmType.Dday && 
                    a.IsDdayPassed).ToList();

                var totalCount = pastAlarms.Count + pastDdays.Count;

                if (totalCount == 0)
                {
                    MessageBox.Show("삭제할 지난 항목이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"지난 알람 {pastAlarms.Count}개, 지난 Dday {pastDdays.Count}개를 모두 삭제하시겠습니까?",
                    "지난 항목 삭제",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var alarm in pastAlarms)
                    {
                        _alarms.Remove(alarm);
                    }
                    foreach (var dday in pastDdays)
                    {
                        _alarms.Remove(dday);
                    }
                    SaveAlarms();
                    RefreshAlarmsList();
                    
                    // DdayWindow도 새로고침
                    if (Application.Current is App app)
                    {
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                        {
                            ddayWindow.RefreshDdayList();
                        }
                    }
                    
                    LogService.LogInfo($"지난 알람 {pastAlarms.Count}개, 지난 Dday {pastDdays.Count}개 삭제 완료");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("지난 항목 삭제 중 오류", ex);
                MessageBox.Show($"지난 항목 삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteAlarms_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var alarms = _alarms.Where(a => a.AlarmType == AlarmType.Alarm).ToList();

                if (alarms.Count == 0)
                {
                    MessageBox.Show("삭제할 알람이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"알람 {alarms.Count}개를 모두 삭제하시겠습니까?",
                    "알람 삭제",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var alarm in alarms)
                    {
                        _alarms.Remove(alarm);
                    }
                    SaveAlarms();
                    RefreshAlarmsList();
                    LogService.LogInfo($"알람 {alarms.Count}개 삭제 완료");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 삭제 중 오류", ex);
                MessageBox.Show($"알람 삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteDdays_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ddays = _alarms.Where(a => a.AlarmType == AlarmType.Dday).ToList();

                if (ddays.Count == 0)
                {
                    MessageBox.Show("삭제할 Dday가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Dday {ddays.Count}개를 모두 삭제하시겠습니까?",
                    "Dday 삭제",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var dday in ddays)
                    {
                        _alarms.Remove(dday);
                    }
                    SaveAlarms();
                    RefreshAlarmsList();
                    
                    // DdayWindow도 새로고침
                    if (Application.Current is App app)
                    {
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                        {
                            ddayWindow.RefreshDdayList();
                        }
                    }
                    
                    LogService.LogInfo($"Dday {ddays.Count}개 삭제 완료");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 삭제 중 오류", ex);
                MessageBox.Show($"Dday 삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var alarms = _alarms.Where(a => a.AlarmType == AlarmType.Alarm).ToList();
                var ddays = _alarms.Where(a => a.AlarmType == AlarmType.Dday).ToList();
                var totalCount = alarms.Count + ddays.Count;

                if (totalCount == 0)
                {
                    MessageBox.Show("삭제할 항목이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"알람 {alarms.Count}개, Dday {ddays.Count}개를 모두 삭제하시겠습니까?",
                    "모든 항목 삭제",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _alarms.Clear();
                    SaveAlarms();
                    RefreshAlarmsList();
                    
                    // DdayWindow도 새로고침
                    if (Application.Current is App app)
                    {
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                        {
                            ddayWindow.RefreshDdayList();
                        }
                    }
                    
                    LogService.LogInfo($"알람 {alarms.Count}개, Dday {ddays.Count}개 삭제 완료");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("모든 항목 삭제 중 오류", ex);
                MessageBox.Show($"모든 항목 삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnableAlarms_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var alarms = _alarms.Where(a => a.AlarmType == AlarmType.Alarm).ToList();
                var count = 0;

                foreach (var alarm in alarms)
                {
                    if (!alarm.IsEnabled)
                    {
                        alarm.IsEnabled = true;
                        count++;
                    }
                }

                if (count > 0)
                {
                    SaveAlarms();
                    RefreshAlarmsList();
                    LogService.LogInfo($"알람 {count}개 활성화 완료");
                }
                else
                {
                    MessageBox.Show("활성화할 알람이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 활성화 중 오류", ex);
                MessageBox.Show($"알람 활성화 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnableDdays_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ddays = _alarms.Where(a => a.AlarmType == AlarmType.Dday).ToList();
                var count = 0;

                foreach (var dday in ddays)
                {
                    if (!dday.IsEnabled)
                    {
                        dday.IsEnabled = true;
                        count++;
                    }
                }

                if (count > 0)
                {
                    SaveAlarms();
                    RefreshAlarmsList();
                    
                    // DdayWindow도 새로고침
                    if (Application.Current is App app)
                    {
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                        {
                            ddayWindow.RefreshDdayList();
                        }
                    }
                    
                    LogService.LogInfo($"Dday {count}개 활성화 완료");
                }
                else
                {
                    MessageBox.Show("활성화할 Dday가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 활성화 중 오류", ex);
                MessageBox.Show($"Dday 활성화 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnableAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var alarms = _alarms.Where(a => a.AlarmType == AlarmType.Alarm).ToList();
                var ddays = _alarms.Where(a => a.AlarmType == AlarmType.Dday).ToList();
                var alarmCount = 0;
                var ddayCount = 0;

                foreach (var alarm in alarms)
                {
                    if (!alarm.IsEnabled)
                    {
                        alarm.IsEnabled = true;
                        alarmCount++;
                    }
                }

                foreach (var dday in ddays)
                {
                    if (!dday.IsEnabled)
                    {
                        dday.IsEnabled = true;
                        ddayCount++;
                    }
                }

                if (alarmCount > 0 || ddayCount > 0)
                {
                    SaveAlarms();
                    RefreshAlarmsList();
                    
                    // DdayWindow도 새로고침
                    if (Application.Current is App app)
                    {
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                        {
                            ddayWindow.RefreshDdayList();
                        }
                    }
                    
                    LogService.LogInfo($"알람 {alarmCount}개, Dday {ddayCount}개 활성화 완료");
                }
                else
                {
                    MessageBox.Show("활성화할 항목이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("모든 항목 활성화 중 오류", ex);
                MessageBox.Show($"모든 항목 활성화 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisableAlarms_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var alarms = _alarms.Where(a => a.AlarmType == AlarmType.Alarm).ToList();
                var count = 0;

                foreach (var alarm in alarms)
                {
                    if (alarm.IsEnabled)
                    {
                        alarm.IsEnabled = false;
                        count++;
                    }
                }

                if (count > 0)
                {
                    SaveAlarms();
                    RefreshAlarmsList();
                    LogService.LogInfo($"알람 {count}개 비활성화 완료");
                }
                else
                {
                    MessageBox.Show("비활성화할 알람이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 비활성화 중 오류", ex);
                MessageBox.Show($"알람 비활성화 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisableDdays_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ddays = _alarms.Where(a => a.AlarmType == AlarmType.Dday).ToList();
                var count = 0;

                foreach (var dday in ddays)
                {
                    if (dday.IsEnabled)
                    {
                        dday.IsEnabled = false;
                        count++;
                    }
                }

                if (count > 0)
                {
                    SaveAlarms();
                    RefreshAlarmsList();
                    
                    // DdayWindow도 새로고침
                    if (Application.Current is App app)
                    {
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                        {
                            ddayWindow.RefreshDdayList();
                        }
                    }
                    
                    LogService.LogInfo($"Dday {count}개 비활성화 완료");
                }
                else
                {
                    MessageBox.Show("비활성화할 Dday가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 비활성화 중 오류", ex);
                MessageBox.Show($"Dday 비활성화 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisableAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var alarms = _alarms.Where(a => a.AlarmType == AlarmType.Alarm).ToList();
                var ddays = _alarms.Where(a => a.AlarmType == AlarmType.Dday).ToList();
                var alarmCount = 0;
                var ddayCount = 0;

                foreach (var alarm in alarms)
                {
                    if (alarm.IsEnabled)
                    {
                        alarm.IsEnabled = false;
                        alarmCount++;
                    }
                }

                foreach (var dday in ddays)
                {
                    if (dday.IsEnabled)
                    {
                        dday.IsEnabled = false;
                        ddayCount++;
                    }
                }

                if (alarmCount > 0 || ddayCount > 0)
                {
                    SaveAlarms();
                    RefreshAlarmsList();
                    
                    // DdayWindow도 새로고침
                    if (Application.Current is App app)
                    {
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                        {
                            ddayWindow.RefreshDdayList();
                        }
                    }
                    
                    LogService.LogInfo($"알람 {alarmCount}개, Dday {ddayCount}개 비활성화 완료");
                }
                else
                {
                    MessageBox.Show("비활성화할 항목이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("모든 항목 비활성화 중 오류", ex);
                MessageBox.Show($"모든 항목 비활성화 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 설정을 다시 로드하여 최신 값을 확인
            var settings = StorageService.LoadSettings();
            if (settings.MinimizeToTray)
            {
                // 트레이로 최소화하도록 설정되어 있으면 창을 닫지 않고 숨김
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                // MinimizeToTray가 false면 정상적으로 종료
                // e.Cancel을 false로 설정하여 창이 닫히도록 함
                e.Cancel = false;
                // 앱 종료 (트레이 아이콘 정리 포함)
                Application.Current.Shutdown();
            }
        }

        public void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }
    }
}

