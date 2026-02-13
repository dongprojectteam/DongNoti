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
        private Alarm? _lastSelectedDday;

        public DdayWindow()
        {
            InitializeComponent();
            _app = Application.Current as App;
            Loaded += DdayWindow_Loaded;
            Closed += DdayWindow_Closed;
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        private void DdayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshDdayList();
            if (Resources["DdayItemContextMenu"] is ContextMenu ctxMenu)
            {
                if (ctxMenu.Items.Count > 0 && ctxMenu.Items[0] is MenuItem registerItem)
                    registerItem.Click += RegisterAsAlarm_Click;
                if (ctxMenu.Items.Count > 1 && ctxMenu.Items[1] is MenuItem editItem)
                    editItem.Click += EditDday_Click;
                if (ctxMenu.Items.Count > 2 && ctxMenu.Items[2] is MenuItem cloneItem)
                    cloneItem.Click += CloneDday_Click;
                if (ctxMenu.Items.Count > 3 && ctxMenu.Items[3] is MenuItem deleteItem)
                    deleteItem.Click += DeleteDday_Click;
            }
            if (_updateTimer != null)
            {
                _updateTimer.Start();
            }
        }

        private void DdayWindow_Closed(object? sender, EventArgs e)
        {
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
                var ddays = alarms
                    .Where(a => a.AlarmType == AlarmType.Dday && a.IsEnabled)
                    .OrderBy(a =>
                    {
                        var d = a.DaysRemaining;
                        if (!d.HasValue) return long.MaxValue;
                        if (d.Value >= 0) return (long)d.Value;
                        return (long)int.MaxValue + Math.Abs(d.Value);
                    })
                    .ToList();
                Alarm? previouslySelectedDday = null;
                if (DdayListBox?.SelectedItem is Alarm selected)
                {
                    previouslySelectedDday = selected;
                }
                if (previouslySelectedDday != null)
                {
                    _lastSelectedDday = previouslySelectedDday;
                }
                Dispatcher.BeginInvoke(() =>
                {
                    if (DdayListBox == null)
                        return;
                    DdayListBox.ItemsSource = ddays;
                    DdayListBox.Items.Refresh();
                    if (EmptyStatePanel != null && DdayListBox != null)
                    {
                        EmptyStatePanel.Visibility = ddays.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        DdayListBox.Visibility = ddays.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                    }
                    if (previouslySelectedDday != null && DdayListBox != null)
                    {
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
                var itemToSelect = DdayListBox.Items.Cast<Alarm>().FirstOrDefault(a => a.Id == ddayToSelect.Id);
                if (itemToSelect != null)
                {
                    DdayListBox.SelectedItem = itemToSelect;
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
            e.Cancel = true;
            Hide();
            if (_app != null)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    _app.UpdateDdayWindowMenuItem();
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
                var source = e.OriginalSource as DependencyObject;
                var listBoxItem = FindParent<ListBoxItem>(source);
                if (listBoxItem != null && listBoxItem.DataContext is Alarm clickedDday)
                {
                    if (_lastSelectedDday != null && _lastSelectedDday.Id == clickedDday.Id)
                    {
                        e.Handled = true;
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
                if (DdayListBox.SelectedItem is Alarm selectedDday)
                {
                    _lastSelectedDday = selectedDday;
                    HideAllMemos();
                    Dispatcher.BeginInvoke(() => ShowSelectedMemo(), DispatcherPriority.Loaded);
                }
                else
                {
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
                            return;
                        }
                    }
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

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            if (_app != null)
            {
                _app.UpdateDdayWindowMenuItem();
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.Dispatcher.BeginInvoke(() =>
                    {
                        mainWindow.UpdateDdayWindowToggleButton();
                    });
                }
            }
            if (_app != null)
            {
                var settings = StorageService.LoadSettings();
                if (settings != null)
                {
                    settings.DdayWindowVisible = false;
                    StorageService.SaveSettings(settings);
                }
            }
        }

        private void RegisterAsAlarm_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var listBoxItem = contextMenu?.PlacementTarget as ListBoxItem;
            var dday = listBoxItem?.DataContext as Alarm;

            if (dday == null)
                return;

            var newAlarm = new Alarm
            {
                Title = dday.Title,
                AlarmType = AlarmType.Alarm,
                DateTime = dday.TargetDate.HasValue
                    ? dday.TargetDate.Value.Date.AddHours(9)
                    : DateTime.Today.AddHours(9),
                Category = dday.Category,
                Priority = dday.Priority
            };

            var dialog = new AlarmDialog(newAlarm)
            {
                Owner = this
            };
            if (dialog.ShowDialog() != true || dialog.Alarm == null)
                return;

            var alarmToAdd = dialog.Alarm;
            try
            {
                var allAlarms = _app?.AlarmService?.GetAlarms() ?? new List<Alarm>();
                allAlarms.Add(alarmToAdd);
                StorageService.SaveAlarms(allAlarms);
                _app?.AlarmService?.RefreshAlarms();
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        _app?.RefreshAlarms(refreshMainWindow: true);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("알람 목록 갱신 중 오류", ex);
                    }
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                LogService.LogError("D-Day에서 알람 등록 후 저장 중 오류", ex);
                MessageBox.Show($"알람 저장 중 오류가 발생했습니다.\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditDday_Click(object sender, RoutedEventArgs e)
        {
            var contextMenu = (sender as MenuItem)?.Parent as ContextMenu;
            var listBoxItem = contextMenu?.PlacementTarget as ListBoxItem;
            var dday = listBoxItem?.DataContext as Alarm;

            if (dday == null)
                return;

            var dialog = new AlarmDialog(dday)
            {
                Owner = this
            };
            if (dialog.ShowDialog() != true || dialog.Alarm == null)
                return;

            try
            {
                var allAlarms = _app?.AlarmService?.GetAlarms() ?? new List<Alarm>();
                var index = allAlarms.FindIndex(a => a.Id == dday.Id);
                if (index < 0)
                {
                    MessageBox.Show("수정할 Dday를 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                allAlarms[index] = dialog.Alarm;
                StorageService.SaveAlarms(allAlarms);
                _app?.AlarmService?.RefreshAlarms();
                RefreshDdayList();
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        _app?.RefreshAlarms(refreshMainWindow: true);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("Dday 수정 후 목록 갱신 중 오류", ex);
                    }
                }, DispatcherPriority.Background);
                LogService.LogInfo($"Dday 수정 완료: '{dialog.Alarm.Title}'");
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 수정 후 저장 중 오류", ex);
                MessageBox.Show($"Dday 수정 저장 중 오류가 발생했습니다.\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloneDday_Click(object sender, RoutedEventArgs e)
        {
            var contextMenu = (sender as MenuItem)?.Parent as ContextMenu;
            var listBoxItem = contextMenu?.PlacementTarget as ListBoxItem;
            var dday = listBoxItem?.DataContext as Alarm;

            if (dday == null)
                return;

            var clone = new Alarm
            {
                Id = Guid.NewGuid().ToString(),
                Title = dday.Title,
                DateTime = dday.DateTime,
                RepeatType = dday.RepeatType,
                IsEnabled = true,
                SoundFilePath = dday.SoundFilePath,
                LastTriggered = null,
                SelectedDaysOfWeek = dday.SelectedDaysOfWeek != null ? new List<DayOfWeek>(dday.SelectedDaysOfWeek) : new List<DayOfWeek>(),
                IsTemporary = dday.IsTemporary,
                AutoDismissMinutes = dday.AutoDismissMinutes,
                Category = dday.Category,
                Priority = dday.Priority,
                AlarmType = AlarmType.Dday,
                TargetDate = dday.TargetDate,
                Memo = dday.Memo
            };

            var dialog = new AlarmDialog(clone, forClone: true)
            {
                Owner = this
            };
            if (dialog.ShowDialog() != true || dialog.Alarm == null)
                return;

            try
            {
                var allAlarms = _app?.AlarmService?.GetAlarms() ?? new List<Alarm>();
                allAlarms.Add(dialog.Alarm);
                StorageService.SaveAlarms(allAlarms);
                _app?.AlarmService?.RefreshAlarms();
                RefreshDdayList();
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        _app?.RefreshAlarms(refreshMainWindow: true);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("Dday 복제 후 목록 갱신 중 오류", ex);
                    }
                }, DispatcherPriority.Background);
                LogService.LogInfo($"Dday 복제 완료: '{dialog.Alarm.Title}'");
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 복제 후 저장 중 오류", ex);
                MessageBox.Show($"Dday 복제 저장 중 오류가 발생했습니다.\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddDdayButton_Click(object sender, RoutedEventArgs e)
        {
            var newDdayTemplate = new Alarm
            {
                AlarmType = AlarmType.Dday,
                Title = "Dday",
                TargetDate = DateTime.Today,
                IsEnabled = true
            };
            var dialog = new AlarmDialog(newDdayTemplate, forClone: true)
            {
                Owner = this
            };
            if (dialog.ShowDialog() != true || dialog.Alarm == null)
                return;

            if (dialog.Alarm.AlarmType != AlarmType.Dday)
                return;

            try
            {
                var allAlarms = _app?.AlarmService?.GetAlarms() ?? new List<Alarm>();
                allAlarms.Add(dialog.Alarm);
                StorageService.SaveAlarms(allAlarms);
                _app?.AlarmService?.RefreshAlarms();
                RefreshDdayList();
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        _app?.RefreshAlarms(refreshMainWindow: true);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("새 Dday 등록 후 목록 갱신 중 오류", ex);
                    }
                }, DispatcherPriority.Background);
                LogService.LogInfo($"Dday 등록 완료: '{dialog.Alarm.Title}'");
            }
            catch (Exception ex)
            {
                LogService.LogError("새 Dday 등록 후 저장 중 오류", ex);
                MessageBox.Show($"Dday 등록 저장 중 오류가 발생했습니다.\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteDday_Click(object sender, RoutedEventArgs e)
        {
            var contextMenu = (sender as MenuItem)?.Parent as ContextMenu;
            var listBoxItem = contextMenu?.PlacementTarget as ListBoxItem;
            var dday = listBoxItem?.DataContext as Alarm;

            if (dday == null)
                return;

            var result = MessageBox.Show(
                $"'{dday.Title}' Dday을(를) 삭제하시겠습니까?",
                "Dday 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                var allAlarms = _app?.AlarmService?.GetAlarms() ?? new List<Alarm>();
                var removed = allAlarms.RemoveAll(a => a.Id == dday.Id);
                if (removed == 0)
                    return;

                StorageService.SaveAlarms(allAlarms);
                _app?.AlarmService?.RefreshAlarms();
                RefreshDdayList();

                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        _app?.RefreshAlarms(refreshMainWindow: true);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("D-Day 삭제 후 목록 갱신 중 오류", ex);
                    }
                }, DispatcherPriority.Background);

                LogService.LogInfo($"Dday 삭제 완료: '{dday.Title}'");
            }
            catch (Exception ex)
            {
                LogService.LogError("D-Day 삭제 중 오류", ex);
                MessageBox.Show($"삭제 중 오류가 발생했습니다.\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
