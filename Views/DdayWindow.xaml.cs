using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DongNoti.Models;
using DongNoti.Services;

namespace DongNoti.Views
{
    public partial class DdayWindow : Window
    {
        private DispatcherTimer? _updateTimer;
        private App? _app;
        private Alarm? _lastSelectedDday; // 이전에 선택된 Dday 추적

        public DdayWindow()
        {
            InitializeComponent();
            
            // App 인스턴스 가져오기
            _app = Application.Current as App;
            
            // 초기 로드
            Loaded += DdayWindow_Loaded;
            Closed += DdayWindow_Closed;
            
            // 실시간 업데이트 타이머 설정 (10초마다)
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        private void DdayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshDdayList();
            
            // 타이머 시작
            if (_updateTimer != null)
            {
                _updateTimer.Start();
            }
        }

        private void DdayWindow_Closed(object? sender, EventArgs e)
        {
            // 타이머 중지
            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer = null;
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            RefreshDdayList();
        }

        public void RefreshDdayList()
        {
            try
            {
                if (_app?.AlarmService == null)
                    return;

                var alarms = _app.AlarmService.GetAlarms();
                
                // Dday 타입만 필터링하고 지난 Dday 제외
                var ddays = alarms
                    .Where(a => a.AlarmType == AlarmType.Dday && !a.IsDdayPassed)
                    .OrderBy(a => a.TargetDate ?? DateTime.MaxValue)
                    .ToList();

                // 현재 선택된 항목 저장 (ID로)
                Alarm? previouslySelectedDday = null;
                if (DdayListBox?.SelectedItem is Alarm selected)
                {
                    previouslySelectedDday = selected;
                }
                // _lastSelectedDday도 업데이트 (RefreshDdayList 후에도 선택 상태 유지)
                if (previouslySelectedDday != null)
                {
                    _lastSelectedDday = previouslySelectedDday;
                }

                // UI 업데이트
                Dispatcher.BeginInvoke(() =>
                {
                    if (DdayListBox == null)
                        return;

                    DdayListBox.ItemsSource = ddays;
                    
                    // 빈 상태 표시
                    if (EmptyStatePanel != null && DdayListBox != null)
                    {
                        EmptyStatePanel.Visibility = ddays.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        DdayListBox.Visibility = ddays.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                    }

                    // 컨테이너가 생성될 때까지 기다린 후 선택 상태 복원 및 메모 표시
                    if (previouslySelectedDday != null && DdayListBox != null)
                    {
                        // 컨테이너 생성 대기
                        if (DdayListBox.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                        {
                            DdayListBox.ItemContainerGenerator.StatusChanged += (s, e) =>
                            {
                                if (DdayListBox != null && DdayListBox.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                                {
                                    RestoreSelectionAndShowMemo(previouslySelectedDday);
                                }
                            };
                        }
                        else
                        {
                            // 이미 생성되어 있으면 즉시 복원
                            Dispatcher.BeginInvoke(() => RestoreSelectionAndShowMemo(previouslySelectedDday), DispatcherPriority.Loaded);
                        }
                    }
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 목록 새로고침 중 오류", ex);
            }
        }

        private void RestoreSelectionAndShowMemo(Alarm ddayToSelect)
        {
            try
            {
                if (DdayListBox == null)
                    return;

                // ID로 항목 찾기
                var itemToSelect = DdayListBox.Items.Cast<Alarm>().FirstOrDefault(a => a.Id == ddayToSelect.Id);
                if (itemToSelect != null)
                {
                    DdayListBox.SelectedItem = itemToSelect;
                    
                    // 메모 표시
                    if (DdayListBox.ItemContainerGenerator.ContainerFromItem(itemToSelect) is System.Windows.Controls.ListBoxItem listBoxItem)
                    {
                        var memoTextBlock = FindVisualChild<System.Windows.Controls.TextBlock>(listBoxItem, "MemoTextBlock");
                        if (memoTextBlock != null && !string.IsNullOrWhiteSpace(itemToSelect.Memo))
                        {
                            memoTextBlock.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("선택 상태 복원 중 오류", ex);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 창을 닫는 대신 숨기기 (실제로는 닫지 않음)
            e.Cancel = true;
            Hide();
            
            // App에 상태 변경 알림 및 상태 저장
            if (_app != null)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    _app.UpdateDdayWindowMenuItem();
                    // 상태 저장 (ShowDdayWindow에서 처리하지만, 여기서도 명시적으로 저장)
                    if (_app is App appInstance)
                    {
                        var settings = StorageService.LoadSettings();
                        settings.DdayWindowVisible = false;
                        StorageService.SaveSettings(settings);
                    }
                    if (_app.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.Dispatcher.BeginInvoke(() =>
                        {
                            mainWindow.UpdateDdayWindowToggleButton();
                        });
                    }
                }, DispatcherPriority.Background);
            }
        }

        private void DdayListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (DdayListBox == null)
                    return;

                // 클릭된 항목 찾기
                var source = e.OriginalSource as DependencyObject;
                var listBoxItem = FindParent<ListBoxItem>(source);
                
                if (listBoxItem != null && listBoxItem.DataContext is Alarm clickedDday)
                {
                    // 같은 항목을 다시 클릭한 경우 (토글)
                    if (_lastSelectedDday != null && _lastSelectedDday.Id == clickedDday.Id)
                    {
                        // 선택 해제하고 메모 숨기기
                        e.Handled = true; // 기본 선택 동작 방지
                        DdayListBox.SelectedItem = null;
                        _lastSelectedDday = null;
                        HideAllMemos();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 항목 클릭 처리 중 오류", ex);
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            var parentObject = child;
            while (parentObject != null)
            {
                if (parentObject is T parent)
                    return parent;
                parentObject = System.Windows.Media.VisualTreeHelper.GetParent(parentObject);
            }
            return null;
        }

        private void DdayListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (DdayListBox == null)
                    return;

                // 현재 선택된 항목 확인
                if (DdayListBox.SelectedItem is Alarm selectedDday)
                {
                    // 다른 항목을 선택한 경우
                    _lastSelectedDday = selectedDday;
                    
                    // 모든 항목의 메모 숨기기
                    HideAllMemos();

                    // 선택된 항목의 메모 표시 (약간의 지연을 두어 컨테이너가 생성될 시간을 줌)
                    Dispatcher.BeginInvoke(() => ShowSelectedMemo(), DispatcherPriority.Loaded);
                }
                else
                {
                    // 선택 해제된 경우
                    _lastSelectedDday = null;
                    HideAllMemos();
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 항목 선택 처리 중 오류", ex);
            }
        }

        private void HideAllMemos()
        {
            try
            {
                if (DdayListBox == null)
                    return;

                foreach (var item in DdayListBox.Items)
                {
                    if (DdayListBox.ItemContainerGenerator.ContainerFromItem(item) is System.Windows.Controls.ListBoxItem listBoxItem)
                    {
                        var memoTextBlock = FindVisualChild<System.Windows.Controls.TextBlock>(listBoxItem, "MemoTextBlock");
                        if (memoTextBlock != null)
                        {
                            memoTextBlock.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("모든 메모 숨기기 중 오류", ex);
            }
        }

        private void ShowSelectedMemo()
        {
            try
            {
                if (DdayListBox == null)
                    return;

                ShowSelectedMemoInternal(0);
            }
            catch (Exception ex)
            {
                LogService.LogError("선택된 메모 표시 중 오류", ex);
            }
        }

        private void ShowSelectedMemoInternal(int retryCount = 0)
        {
            try
            {
                if (DdayListBox?.SelectedItem is Alarm selectedDday && !string.IsNullOrWhiteSpace(selectedDday.Memo))
                {
                    var container = DdayListBox.ItemContainerGenerator.ContainerFromItem(selectedDday);
                    if (container is System.Windows.Controls.ListBoxItem selectedItem)
                    {
                        var memoTextBlock = FindVisualChild<System.Windows.Controls.TextBlock>(selectedItem, "MemoTextBlock");
                        if (memoTextBlock != null)
                        {
                            memoTextBlock.Visibility = Visibility.Visible;
                            return; // 성공적으로 표시됨
                        }
                    }

                    // 컨테이너가 아직 생성되지 않았으면 재시도 (최대 5번)
                    if (retryCount < 5 && DdayListBox.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                    {
                        Dispatcher.BeginInvoke(() => ShowSelectedMemoInternal(retryCount + 1), DispatcherPriority.Loaded);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("메모 표시 내부 처리 중 오류", ex);
            }
        }

        private static T? FindVisualChild<T>(System.Windows.DependencyObject parent, string name) where T : System.Windows.DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t && (child as System.Windows.FrameworkElement)?.Name == name)
                {
                    return t;
                }
                
                var childOfChild = FindVisualChild<T>(child, name);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }
    }
}
