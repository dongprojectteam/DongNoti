using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DongNoti.Models;
using DongNoti.Services;

namespace DongNoti.Views
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings = null!;
        private DispatcherTimer? _updateTimer;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLogBufferCount();
            
            // 1초마다 버퍼 개수 업데이트
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(1);
            _updateTimer.Tick += (s, args) => UpdateLogBufferCount();
            _updateTimer.Start();
        }

        private void LoadSettings()
        {
            _settings = StorageService.LoadSettings();
            RunOnStartupCheckBox.IsChecked = _settings.RunOnStartup;
            HideToTrayOnStartupCheckBox.IsChecked = _settings.HideToTrayOnStartup;
            MinimizeToTrayCheckBox.IsChecked = _settings.MinimizeToTray;
            EnableLoggingCheckBox.IsChecked = _settings.EnableLogging;
            ShowUILogCheckBox.IsChecked = _settings.ShowUILog;
            
            // 프리셋 목록 로드
            LoadPresets();
            
            // 카테고리 목록 로드
            LoadCategories();
        }

        private void LoadCategories()
        {
            try
            {
                var categories = _settings.AlarmCategories ?? new List<string> { "기본", "업무", "개인", "약속" };
                
                // 기본 카테고리가 없으면 추가
                if (!categories.Contains("기본"))
                {
                    categories.Insert(0, "기본");
                }
                
                CategoriesItemsControl.ItemsSource = categories;
            }
            catch (Exception ex)
            {
                LogService.LogError("카테고리 목록 로드 중 오류", ex);
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newCategory = NewCategoryTextBox?.Text?.Trim() ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(newCategory))
                {
                    MessageBox.Show("카테고리 이름을 입력해주세요.", 
                                   "알림", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Information);
                    return;
                }

                if (newCategory == "기본")
                {
                    MessageBox.Show("'기본' 카테고리는 이미 존재합니다.", 
                                   "알림", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Information);
                    return;
                }

                var categories = _settings.AlarmCategories ?? new List<string> { "기본", "업무", "개인", "약속" };
                
                if (categories.Contains(newCategory))
                {
                    MessageBox.Show("이미 존재하는 카테고리입니다.", 
                                   "알림", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Information);
                    return;
                }

                categories.Add(newCategory);
                _settings.AlarmCategories = categories;
                StorageService.SaveSettings(_settings);
                
                LoadCategories();
                if (NewCategoryTextBox != null)
                {
                    NewCategoryTextBox.Text = string.Empty;
                }
                
                MessageBox.Show($"카테고리 '{newCategory}'이 추가되었습니다.", 
                              "완료", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogService.LogError("카테고리 추가 중 오류", ex);
                MessageBox.Show($"카테고리 추가 중 오류가 발생했습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void ResetCategories_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "카테고리를 기본값으로 초기화하시겠습니까?\n\n" +
                    "기본 카테고리: 기본, 업무, 개인, 약속\n\n" +
                    "사라지는 카테고리를 사용하는 알람들은 모두 '기본' 카테고리로 변경됩니다.",
                    "카테고리 초기화",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 기본 카테고리 목록
                    var defaultCategories = new List<string> { "기본", "업무", "개인", "약속" };
                    
                    // 현재 카테고리 목록
                    var currentCategories = _settings.AlarmCategories ?? new List<string> { "기본", "업무", "개인", "약속" };
                    
                    // 사라질 카테고리들 찾기
                    var removedCategories = currentCategories.Where(c => c != "기본" && !defaultCategories.Contains(c)).ToList();
                    
                    // 알람들의 카테고리를 "기본"으로 변경
                    if (removedCategories.Count > 0)
                    {
                        var alarms = StorageService.LoadAlarms();
                        bool hasChanges = false;
                        foreach (var alarm in alarms)
                        {
                            if (alarm.Category != null && removedCategories.Contains(alarm.Category))
                            {
                                alarm.Category = null; // null = "기본"
                                hasChanges = true;
                            }
                        }
                        if (hasChanges)
                        {
                            StorageService.SaveAlarms(alarms);
                            if (Application.Current is App app)
                            {
                                app.RefreshAlarms(refreshMainWindow: true);
                            }
                        }
                    }
                    
                    // 카테고리 목록 초기화
                    _settings.AlarmCategories = defaultCategories;
                    StorageService.SaveSettings(_settings);
                    
                    LoadCategories();
                    
                    MessageBox.Show("카테고리가 기본값으로 초기화되었습니다.", 
                                  "완료", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("카테고리 초기화 중 오류", ex);
                MessageBox.Show($"카테고리 초기화 중 오류가 발생했습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is string category)
                {
                    if (category == "기본")
                    {
                        MessageBox.Show("'기본' 카테고리는 삭제할 수 없습니다.", 
                                      "알림", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Information);
                        return;
                    }

                    var result = MessageBox.Show(
                        $"카테고리 '{category}'을 삭제하시겠습니까?\n\n이 카테고리를 사용하는 알람은 '기본' 카테고리로 변경됩니다.",
                        "카테고리 삭제",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var categories = _settings.AlarmCategories ?? new List<string> { "기본", "업무", "개인", "약속" };
                        categories.Remove(category);
                        _settings.AlarmCategories = categories;
                        StorageService.SaveSettings(_settings);
                        
                        // 해당 카테고리를 사용하는 알람들을 '기본'으로 변경
                        var alarms = StorageService.LoadAlarms();
                        bool hasChanges = false;
                        foreach (var alarm in alarms)
                        {
                            if (alarm.Category == category)
                            {
                                alarm.Category = null; // null = "기본"
                                hasChanges = true;
                            }
                        }
                        if (hasChanges)
                        {
                            StorageService.SaveAlarms(alarms);
                            if (Application.Current is App app)
                            {
                                app.RefreshAlarms(refreshMainWindow: true);
                            }
                        }
                        
                        LoadCategories();
                        
                        MessageBox.Show("카테고리가 삭제되었습니다.", 
                                      "완료", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("카테고리 삭제 중 오류", ex);
                MessageBox.Show($"카테고리 삭제 중 오류가 발생했습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void LoadPresets()
        {
            try
            {
                var presets = _settings.FocusModePresets ?? AppSettings.GetDefaultPresets();
                
                // 기본 프리셋이 없으면 초기화
                if (presets.Count == 0)
                {
                    presets = AppSettings.GetDefaultPresets();
                    _settings.FocusModePresets = presets;
                    if (string.IsNullOrEmpty(_settings.DefaultFocusModePresetId))
                    {
                        _settings.DefaultFocusModePresetId = "30m";
                    }
                }
                
                // 각 프리셋에 IsDefault 속성 추가
                var presetViewModels = presets.Select(p => new PresetViewModel
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    Minutes = p.Minutes,
                    IsDefault = p.Id == _settings.DefaultFocusModePresetId
                }).ToList();
                
                PresetsItemsControl.ItemsSource = presetViewModels;
            }
            catch (Exception ex)
            {
                LogService.LogError("프리셋 목록 로드 중 오류", ex);
                MessageBox.Show($"프리셋 목록을 로드하는 중 오류가 발생했습니다: {ex.Message}",
                              "오류",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private class PresetViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public int Minutes { get; set; }
            public bool IsDefault { get; set; }
        }


        private void UpdateLogBufferCount()
        {
            LogBufferCountText.Text = $"(버퍼: {LogService.BufferedLogCount}개)";
        }

        private void FlushLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var count = LogService.BufferedLogCount;
                LogService.Flush();
                MessageBox.Show($"{count}개의 로그를 파일로 저장했습니다.", 
                               "로그 저장", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Information);
                UpdateLogBufferCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"로그 저장 중 오류: {ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dataFolder = StorageService.GetDataDirectory();
                
                // 폴더가 없으면 생성
                if (!Directory.Exists(dataFolder))
                {
                    Directory.CreateDirectory(dataFolder);
                }
                
                // Windows 탐색기로 폴더 열기
                Process.Start(new ProcessStartInfo
                {
                    FileName = dataFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogService.LogError("데이터 폴더 열기 중 오류", ex);
                MessageBox.Show($"데이터 폴더를 열 수 없습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void OpenLogFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logFilePath = LogService.GetCurrentLogFilePath();
                if (File.Exists(logFilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = logFilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("로그 파일이 아직 생성되지 않았습니다.", 
                                   "알림", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"로그 파일 열기 중 오류: {ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void RunOnStartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            StartupService.SetStartup(true);
        }

        private void RunOnStartupCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            StartupService.SetStartup(false);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            _settings.RunOnStartup = RunOnStartupCheckBox.IsChecked ?? false;
            _settings.HideToTrayOnStartup = HideToTrayOnStartupCheckBox.IsChecked ?? false;
            _settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? false;
            _settings.EnableLogging = EnableLoggingCheckBox.IsChecked ?? true;
            _settings.ShowUILog = ShowUILogCheckBox.IsChecked ?? false;

            // 카테고리 목록 저장
            if (CategoriesItemsControl.ItemsSource is IEnumerable<string> categories)
            {
                _settings.AlarmCategories = categories.ToList();
            }

            // 프리셋 목록 저장 (ViewModel에서 실제 모델로 변환)
            if (PresetsItemsControl.ItemsSource is IEnumerable<PresetViewModel> presetViewModels)
            {
                var presets = presetViewModels.Select(vm => new FocusModePreset
                {
                    Id = vm.Id,
                    DisplayName = vm.DisplayName,
                    Minutes = vm.Minutes
                }).ToList();
                _settings.FocusModePresets = presets;
                
                // 기본 프리셋 ID 저장
                var defaultPreset = presetViewModels.FirstOrDefault(vm => vm.IsDefault);
                if (defaultPreset != null)
                {
                    _settings.DefaultFocusModePresetId = defaultPreset.Id;
                }
            }

            StorageService.SaveSettings(_settings);

            // 로그 서비스 설정 업데이트
            LogService.SetEnabled(_settings.EnableLogging);

            // UI 로그 창 설정 업데이트
            if (Application.Current is App app)
            {
                app.SetUILogEnabled(_settings.ShowUILog);
            }

            // 자동 실행 설정 업데이트
            StartupService.SetStartup(_settings.RunOnStartup);

            DialogResult = true;
            Close();
        }

        private void ShowUILogCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.SetUILogEnabled(true);
            }
        }

        private void ShowUILogCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.SetUILogEnabled(false);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ResetPresets_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "집중 모드 프리셋을 기본값으로 초기화하시겠습니까?\n\n" +
                    "기본 프리셋: 30분, 1시간, 1시간 30분, 2시간, 2시간 30분, 3시간",
                    "프리셋 초기화",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _settings.FocusModePresets = AppSettings.GetDefaultPresets();
                    _settings.DefaultFocusModePresetId = "30m";
                    StorageService.SaveSettings(_settings);
                    LoadPresets();

                    MessageBox.Show("프리셋이 기본값으로 초기화되었습니다.",
                                  "완료",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"프리셋 초기화 중 오류: {ex.Message}",
                              "오류",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void AddPreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 시간 입력 다이얼로그
                var inputDialog = new InputDialog
                {
                    Title = "프리셋 추가",
                    Prompt = "시간(분)을 입력하세요:",
                    Owner = this
                };

                if (inputDialog.ShowDialog() == true)
                {
                    if (int.TryParse(inputDialog.Answer, out int minutes) && minutes > 0)
                    {
                        // 중복 체크
                        var existingPresets = _settings.FocusModePresets ?? new List<FocusModePreset>();
                        if (existingPresets.Any(p => p.Minutes == minutes))
                        {
                            MessageBox.Show("이미 동일한 시간의 프리셋이 있습니다.",
                                          "알림",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Information);
                            return;
                        }

                        // 새 프리셋 생성
                        var hours = minutes / 60;
                        var remainingMinutes = minutes % 60;
                        string displayName;
                        
                        if (hours > 0 && remainingMinutes > 0)
                        {
                            displayName = $"{hours}시간 {remainingMinutes}분";
                        }
                        else if (hours > 0)
                        {
                            displayName = $"{hours}시간";
                        }
                        else
                        {
                            displayName = $"{minutes}분";
                        }

                        var newPreset = new FocusModePreset
                        {
                            Id = Guid.NewGuid().ToString(),
                            DisplayName = displayName,
                            Minutes = minutes
                        };

                        existingPresets.Add(newPreset);
                        _settings.FocusModePresets = existingPresets;
                        StorageService.SaveSettings(_settings);
                        LoadPresets();

                        MessageBox.Show($"프리셋 '{displayName}'이 추가되었습니다.",
                                      "완료",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("올바른 숫자를 입력해주세요 (1 이상).",
                                      "오류",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"프리셋 추가 중 오류: {ex.Message}",
                              "오류",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is PresetViewModel preset)
                {
                    // 기본 프리셋은 삭제 불가
                    if (preset.IsDefault)
                    {
                        MessageBox.Show("기본 프리셋은 삭제할 수 없습니다.\n다른 프리셋을 기본값으로 설정한 후 삭제해주세요.",
                                      "알림",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                        return;
                    }

                    var result = MessageBox.Show(
                        $"프리셋 '{preset.DisplayName}'을 삭제하시겠습니까?",
                        "프리셋 삭제",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var presets = _settings.FocusModePresets ?? new List<FocusModePreset>();
                        presets.RemoveAll(p => p.Id == preset.Id);
                        _settings.FocusModePresets = presets;
                        StorageService.SaveSettings(_settings);
                        LoadPresets();

                        MessageBox.Show("프리셋이 삭제되었습니다.",
                                      "완료",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"프리셋 삭제 중 오류: {ex.Message}",
                              "오류",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void DefaultPreset_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is RadioButton radioButton && radioButton.DataContext is PresetViewModel preset)
                {
                    _settings.DefaultFocusModePresetId = preset.Id;
                    StorageService.SaveSettings(_settings);
                    LoadPresets(); // UI 업데이트
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("기본 프리셋 설정 중 오류", ex);
            }
        }

        private void ExportAlarms_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 현재 알람 목록 가져오기
                var alarms = StorageService.LoadAlarms();
                
                if (alarms == null || alarms.Count == 0)
                {
                    MessageBox.Show("내보낼 알람이 없습니다.", 
                                   "알림", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Information);
                    return;
                }

                // 내보내기 실행
                if (StorageService.ExportAlarms(alarms))
                {
                    MessageBox.Show($"{alarms.Count}개의 알람을 내보냈습니다.", 
                                   "완료", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 내보내기 중 오류", ex);
                MessageBox.Show($"알람 내보내기 중 오류가 발생했습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void ImportAlarms_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 파일에서 알람 가져오기
                var importedAlarms = StorageService.ImportAlarms();
                
                if (importedAlarms == null)
                {
                    return; // 사용자가 취소했거나 오류 발생
                }

                if (importedAlarms.Count == 0)
                {
                    MessageBox.Show("가져올 알람이 없습니다.", 
                                   "알림", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Information);
                    return;
                }

                // 기존 알람과 병합할지 교체할지 선택
                var currentAlarms = StorageService.LoadAlarms();
                // LoadAlarms()는 null을 반환하지 않지만, Nullable 분석/미래 변경에 대비해 방어적으로 처리
                currentAlarms ??= new List<Alarm>();
                var hasExistingAlarms = currentAlarms.Count > 0;

                if (hasExistingAlarms)
                {
                    var result = MessageBox.Show(
                        $"현재 {currentAlarms.Count}개의 알람이 있습니다.\n" +
                        $"가져온 {importedAlarms.Count}개의 알람을 어떻게 처리하시겠습니까?\n\n" +
                        "예: 기존 알람과 병합\n" +
                        "아니오: 기존 알람을 모두 교체\n" +
                        "취소: 가져오기 취소",
                        "알람 가져오기",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                    else if (result == MessageBoxResult.Yes)
                    {
                        // 병합: 중복 제거 (ID 기준)
                        var mergedAlarms = new List<Alarm>(currentAlarms);
                        foreach (var importedAlarm in importedAlarms)
                        {
                            if (!mergedAlarms.Any(a => a.Id == importedAlarm.Id))
                            {
                                mergedAlarms.Add(importedAlarm);
                            }
                        }
                        StorageService.SaveAlarms(mergedAlarms);
                        MessageBox.Show($"알람 병합 완료: 총 {mergedAlarms.Count}개 (기존 {currentAlarms.Count}개 + 신규 {importedAlarms.Count - (importedAlarms.Count - (mergedAlarms.Count - currentAlarms.Count))}개)", 
                                       "완료", 
                                       MessageBoxButton.OK, 
                                       MessageBoxImage.Information);
                    }
                    else // No
                    {
                        // 교체
                        StorageService.SaveAlarms(importedAlarms);
                        MessageBox.Show($"알람 교체 완료: {importedAlarms.Count}개", 
                                       "완료", 
                                       MessageBoxButton.OK, 
                                       MessageBoxImage.Information);
                    }
                }
                else
                {
                    // 기존 알람이 없으면 그냥 저장
                    StorageService.SaveAlarms(importedAlarms);
                    MessageBox.Show($"{importedAlarms.Count}개의 알람을 가져왔습니다.", 
                                   "완료", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Information);
                }

                // AlarmService 새로고침
                if (Application.Current is App app)
                {
                    app.RefreshAlarms(refreshMainWindow: true);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 가져오기 중 오류", ex);
                MessageBox.Show($"알람 가져오기 중 오류가 발생했습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Stop();
            base.OnClosed(e);
        }
    }
}

