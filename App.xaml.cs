using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using DongNoti.Models;
using DongNoti.Services;
using DongNoti.Views;

namespace DongNoti
{
    internal static class User32
    {
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        internal static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
        
        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        
        internal const int SW_RESTORE = 9;
        internal const int SW_SHOW = 5;
    }

    public partial class App : Application
    {
        private TaskbarIcon? _taskbarIcon;
        private MainWindow? _mainWindow;
        private AlarmService? _alarmService;
        private NotificationService? _notificationService;
        private SoundService? _soundService;
        private ContextMenu? _contextMenu;
        private Views.LogWindow? _logWindow;
        private Views.DdayWindow? _ddayWindow;
        private MenuItem? _ddayWindowMenuItem;
        private static Mutex? _mutex;
        private const string MutexName = "DongNoti_SingleInstance_Mutex";
        private const string PipeName = "DongNoti_SingleInstance_Pipe";
        private Task? _pipeServerTask;
        private CancellationTokenSource? _pipeServerCancellation;
        
        /// <summary>
        /// AlarmService 인스턴스에 접근하기 위한 프로퍼티
        /// </summary>
        public AlarmService? AlarmService => _alarmService;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);
            
            if (!createdNew)
            {
                try
                {
                    using (var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                    {
                        try
                        {
                            pipeClient.Connect(500); // 500ms 타임아웃
                            using (var writer = new StreamWriter(pipeClient))
                            {
                                writer.WriteLine("ShowMainWindow");
                                writer.Flush();
                            }
                        }
                        catch (TimeoutException) { }
                        catch { }
                    }
                }
                catch { }
                Shutdown();
                return;
            }
            StartPipeServer();
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                try
                {
                    var logDir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "DongNoti", "Logs");
                    if (!System.IO.Directory.Exists(logDir))
                        System.IO.Directory.CreateDirectory(logDir);
                    
                    var logFile = System.IO.Path.Combine(logDir, $"DongNoti_Crash_{DateTime.Now:yyyy-MM-dd}.log");
                    var errorMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL ERROR: {args.ExceptionObject}";
                    System.IO.File.AppendAllText(logFile, errorMsg + Environment.NewLine, System.Text.Encoding.UTF8);
                }
                catch { }
            };

            base.OnStartup(e);

            try
            {
                LogService.Initialize();
                var settings = StorageService.LoadSettings();
                LogService.SetEnabled(settings.EnableLogging);
                if (settings.ShowUILog)
                {
                    InitializeLogWindow();
                }
                
                LogService.LogInfo("=== DongNoti 앱 시작 ===");
                LogService.LogInfo($"로그 활성화: {settings.EnableLogging}");
                LogService.LogInfo($"UI 로그 활성화: {settings.ShowUILog}");
                LogService.LogInfo("서비스 초기화 시작");
                _notificationService = new NotificationService();
                _soundService = new SoundService();
                _alarmService = new AlarmService();
                _alarmService.AlarmTriggered += OnAlarmTriggered;
                LogService.LogInfo("서비스 초기화 완료");
                _taskbarIcon = new TaskbarIcon
                {
                    Icon = new System.Drawing.Icon(Application.GetResourceStream(
                        new Uri("pack://application:,,,/Resources/tray.ico")).Stream),
                    ToolTipText = "DongNoti 알람",
                    Visibility = Visibility.Visible
                };
                CreateContextMenu();
                _taskbarIcon.TrayMouseDoubleClick += (s, args) => ShowMainWindow();
                try
                {
                    LogService.LogInfo("MainWindow 생성 시작");
                    _mainWindow = new MainWindow();
                    LogService.LogInfo("MainWindow 인스턴스 생성 완료");
                    
                    MainWindow = _mainWindow;
                    LogService.LogInfo("MainWindow 속성 설정 완료");
                    _mainWindow.LoadAlarms();
                    if (settings.HideToTrayOnStartup)
                    {
                        LogService.LogInfo("트레이로 숨김 설정");
                        _mainWindow.WindowState = WindowState.Minimized;
                        _mainWindow.Hide();
                    }
                    else
                    {
                        LogService.LogInfo("MainWindow 표시");
                        _mainWindow.Show();
                    }
                    
                    LogService.LogInfo("MainWindow 생성 완료");
                    if (settings.DdayWindowVisible)
                    {
                        LogService.LogInfo("Dday 창 상태 복원: 표시");
                        Dispatcher.BeginInvoke(() =>
                        {
                            ShowDdayWindow();
                        }, System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        LogService.LogError("MainWindow 생성 중 오류", ex);
                    }
                    catch
                    {
                        try
                        {
                            var logDir = System.IO.Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "DongNoti", "Logs");
                            if (!System.IO.Directory.Exists(logDir))
                                System.IO.Directory.CreateDirectory(logDir);
                            
                            var logFile = System.IO.Path.Combine(logDir, $"DongNoti_Crash_{DateTime.Now:yyyy-MM-dd}.log");
                            var errorMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow 생성 오류: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}";
                            System.IO.File.AppendAllText(logFile, errorMsg + Environment.NewLine, System.Text.Encoding.UTF8);
                        }
                        catch { }
                    }
                    throw;
                }

                LogService.LogInfo("앱 초기화 완료");
            }
            catch (Exception ex)
            {
                LogService.LogError("앱 시작 중 치명적 오류 발생", ex);
                throw;
            }
        }

        private void OnAlarmTriggered(Alarm alarm)
        {
            LogService.LogInfo($"알람 트리거 이벤트 수신: '{alarm.Title}'");
            Dispatcher.Invoke(() =>
            {
                try
                {
                    _notificationService?.ShowAlarmNotification(alarm);
                    var popup = new AlarmPopup(alarm, _soundService!);
                    popup.Show();
                    LogService.LogInfo($"알람 팝업 창 표시: '{alarm.Title}'");
                }
                catch (Exception ex)
                {
                    LogService.LogError($"알람 트리거 처리 중 오류: '{alarm.Title}'", ex);
                }
            });
        }

        public void RefreshAlarms(bool refreshMainWindow = false)
        {
            try
            {
                _alarmService?.RefreshAlarms();
                if (refreshMainWindow && _mainWindow != null)
                {
                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            _mainWindow.RefreshAlarmsList();
                        }
                        catch (Exception ex)
                        {
                            LogService.LogError("MainWindow.RefreshAlarmsList 중 오류", ex);
                        }
                    }, System.Windows.Threading.DispatcherPriority.Normal);
                }
                
                if (_contextMenu != null && _contextMenu.Items != null && _contextMenu.Items.Count > 0)
                {
                    var alarmsMenu = _contextMenu.Items[0] as MenuItem;
                    if (alarmsMenu != null)
                    {
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            UpdateAlarmsMenu(alarmsMenu);
                        });
                    }
                    var ddaysMenu = _contextMenu.Items.OfType<MenuItem>()
                        .FirstOrDefault(m => m.Header?.ToString() == "Dday");
                    if (ddaysMenu != null)
                    {
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            UpdateDdaysMenu(ddaysMenu);
                        });
                    }
                }
                if (_ddayWindow != null && _ddayWindow.IsVisible)
                {
                    _ddayWindow.Dispatcher.BeginInvoke(() =>
                    {
                        _ddayWindow.RefreshDdayList();
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("RefreshAlarms 중 오류", ex);
            }
        }

        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenu();
            var alarmsMenu = new MenuItem { Header = "알람" };
            UpdateAlarmsMenu(alarmsMenu);
            _contextMenu.Items.Add(alarmsMenu);
            var ddaysMenu = new MenuItem { Header = "Dday" };
            UpdateDdaysMenu(ddaysMenu);
            _contextMenu.Items.Add(ddaysMenu);

            _contextMenu.Items.Add(new Separator());
            var focusModeMenu = new MenuItem { Header = "🌙 집중 모드" };
            UpdateFocusModeMenu(focusModeMenu);
            _contextMenu.Items.Add(focusModeMenu);

            _contextMenu.Items.Add(new Separator());
            var showWindowItem = new MenuItem { Header = "앱 화면 열기" };
            showWindowItem.Click += (s, e) => ShowMainWindow();
            _contextMenu.Items.Add(showWindowItem);
            _ddayWindowMenuItem = new MenuItem { Header = "Dday창 표시" };
            _ddayWindowMenuItem.Click += (s, e) => ShowDdayWindow();
            _contextMenu.Items.Add(_ddayWindowMenuItem);
            UpdateDdayWindowMenuItem();
            var settingsItem = new MenuItem { Header = "설정" };
            settingsItem.Click += (s, e) =>
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
                RefreshAlarms(refreshMainWindow: true);
            };
            _contextMenu.Items.Add(settingsItem);

            _contextMenu.Items.Add(new Separator());
            var exitItem = new MenuItem { Header = "종료" };
            exitItem.Click += (s, e) => Shutdown();
            _contextMenu.Items.Add(exitItem);

            if (_taskbarIcon != null)
            {
                _taskbarIcon.ContextMenu = _contextMenu;
            }
            FocusModeService.Instance.FocusModeChanged += (isActive) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    UpdateFocusModeMenu(focusModeMenu);
                    UpdateTrayIconTooltip();
                });
            };

            FocusModeService.Instance.FocusModeEnded += () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    var missedAlarms = FocusModeService.Instance.GetMissedAlarms();
                    if (missedAlarms.Count > 0)
                    {
                        var summaryWindow = new MissedAlarmsSummaryWindow();
                        summaryWindow.Show();
                    }
                });
            };
        }

        private void UpdateAlarmsMenu(MenuItem parentMenu)
        {
            try
            {
                if (parentMenu == null || parentMenu.Items == null)
                    return;
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var allAlarms = _alarmService?.GetAlarms() ?? new List<Alarm>();
                        var alarms = allAlarms
                            .Where(a => a.AlarmType == AlarmType.Alarm)
                            .OrderBy(a => a.DateTime)
                            .ToList();
                        Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                parentMenu.Items.Clear();

                                if (alarms == null || alarms.Count == 0)
                                {
                                    var noAlarmsItem = new MenuItem { Header = "알람이 없습니다", IsEnabled = false };
                                    parentMenu.Items.Add(noAlarmsItem);
                                    return;
                                }

                                foreach (var alarm in alarms)
                                {
                                    if (alarm == null) continue;

                                    var alarmItem = new MenuItem
                                    {
                                        Header = $"{alarm.Title} - {alarm.DateTime:HH:mm}",
                                        IsCheckable = true,
                                        IsChecked = alarm.IsEnabled
                                    };
                                    var alarmCopy = alarm;
                                    alarmItem.Checked += (s, e) =>
                                    {
                                        try
                                        {
                                            alarmCopy.IsEnabled = true;
                                            System.Threading.Tasks.Task.Run(() =>
                                            {
                                                StorageService.SaveAlarms(allAlarms);
                                                _alarmService?.RefreshAlarms();
                                                UpdateAlarmsMenu(parentMenu);
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            LogService.LogError("알람 활성화 중 오류", ex);
                                        }
                                    };

                                    alarmItem.Unchecked += (s, e) =>
                                    {
                                        try
                                        {
                                            alarmCopy.IsEnabled = false;
                                            System.Threading.Tasks.Task.Run(() =>
                                            {
                                                StorageService.SaveAlarms(allAlarms);
                                                _alarmService?.RefreshAlarms();
                                                UpdateAlarmsMenu(parentMenu);
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            LogService.LogError("알람 비활성화 중 오류", ex);
                                        }
                                    };

                                    parentMenu.Items.Add(alarmItem);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogService.LogError("UpdateAlarmsMenu UI 업데이트 중 오류", ex);
                            }
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("UpdateAlarmsMenu 로드 중 오류", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.LogError("UpdateAlarmsMenu 중 오류", ex);
            }
        }

        private void UpdateFocusModeMenu(MenuItem parentMenu)
        {
            try
            {
                if (parentMenu == null || parentMenu.Items == null)
                    return;

                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        parentMenu.Items.Clear();

                        if (FocusModeService.Instance.IsFocusModeActive)
                        {
                            var statusItem = new MenuItem 
                            { 
                                Header = $"✅ 활성화 (남은 시간: {TimeHelper.FormatTimeSpan(FocusModeService.Instance.GetRemainingTime())})",
                                IsEnabled = false
                            };
                            parentMenu.Items.Add(statusItem);
                            
                            parentMenu.Items.Add(new Separator());
                            var stopItem = new MenuItem { Header = "종료" };
                            stopItem.Click += (s, e) =>
                            {
                                FocusModeService.Instance.StopFocusMode();
                            };
                            parentMenu.Items.Add(stopItem);
                        }
                        else
                        {
                            var presets = FocusModeService.Instance.GetPresets();
                            
                            foreach (var preset in presets)
                            {
                                var presetItem = new MenuItem { Header = preset.DisplayName };
                                var presetCopy = preset;
                                presetItem.Click += (s, e) =>
                                {
                                    try
                                    {
                                        FocusModeService.Instance.StartFocusMode(presetCopy.Minutes);
                                        LogService.LogInfo($"트레이에서 집중모드 시작: {presetCopy.DisplayName}");
                                    }
                                    catch (Exception ex)
                                    {
                                        LogService.LogError("트레이에서 집중모드 시작 중 오류", ex);
                                    }
                                };
                                parentMenu.Items.Add(presetItem);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("UpdateFocusModeMenu UI 업데이트 중 오류", ex);
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                LogService.LogError("UpdateFocusModeMenu 중 오류", ex);
            }
        }

        private void UpdateDdaysMenu(MenuItem parentMenu)
        {
            try
            {
                if (parentMenu == null || parentMenu.Items == null)
                    return;
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var alarms = _alarmService?.GetAlarms() ?? new List<Alarm>();
                        var ddays = alarms
                            .Where(a => a.AlarmType == AlarmType.Dday && !a.IsDdayPassed && a.IsEnabled)
                            .OrderBy(a => a.TargetDate ?? DateTime.MaxValue)
                            .ToList();
                        Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                parentMenu.Items.Clear();

                                if (ddays == null || ddays.Count == 0)
                                {
                                    var noDdaysItem = new MenuItem { Header = "Dday가 없습니다", IsEnabled = false };
                                    parentMenu.Items.Add(noDdaysItem);
                                    return;
                                }

                                foreach (var dday in ddays)
                                {
                                    if (dday == null) continue;

                                    var ddayItem = new MenuItem
                                    {
                                        Header = $"{dday.Title} - {dday.DdayDisplayString}"
                                    };

                                    parentMenu.Items.Add(ddayItem);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogService.LogError("UpdateDdaysMenu UI 업데이트 중 오류", ex);
                            }
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("UpdateDdaysMenu 로드 중 오류", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.LogError("UpdateDdaysMenu 중 오류", ex);
            }
        }

        private void UpdateTrayIconTooltip()
        {
            try
            {
                if (_taskbarIcon != null)
                {
                    if (FocusModeService.Instance.IsFocusModeActive)
                    {
                        var remaining = FocusModeService.Instance.GetRemainingTime();
                        _taskbarIcon.ToolTipText = $"DongNoti 알람\n🌙 집중 모드 ({TimeHelper.FormatTimeSpan(remaining)} 남음)";
                    }
                    else
                    {
                        _taskbarIcon.ToolTipText = "DongNoti 알람";
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("트레이 아이콘 툴팁 업데이트 중 오류", ex);
            }
        }

        private void App_Activated(object? sender, EventArgs e)
        {
            if (_mainWindow == null && MainWindow is MainWindow mainWin)
            {
                _mainWindow = mainWin;
            }
        }

        private void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                if (MainWindow is MainWindow mainWin)
                {
                    _mainWindow = mainWin;
                }
                else
                {
                    _mainWindow = new MainWindow();
                    _mainWindow.Show();
                }
            }

            if (_mainWindow != null)
            {
                _mainWindow.ShowWindow();
            }
        }

        public Views.DdayWindow? GetDdayWindow()
        {
            return _ddayWindow;
        }

        public void ShowDdayWindow()
        {
            bool wasVisible = _ddayWindow != null && _ddayWindow.IsVisible;
            
            if (_ddayWindow == null)
            {
                _ddayWindow = new Views.DdayWindow();
                _ddayWindow.Show();
                _ddayWindow.Activate();
            }
            else if (!wasVisible)
            {
                _ddayWindow.RefreshDdayList();
                _ddayWindow.Show();
                _ddayWindow.Activate();
            }
            else
            {
                _ddayWindow.Hide();
            }
            SaveDdayWindowState();
            Dispatcher.BeginInvoke(() =>
            {
                UpdateDdayWindowMenuItem();
            }, System.Windows.Threading.DispatcherPriority.Normal);
            if (_mainWindow != null)
            {
                _mainWindow.Dispatcher.BeginInvoke(() =>
                {
                    _mainWindow.UpdateDdayWindowToggleButton();
                });
            }
        }

        private void SaveDdayWindowState()
        {
            try
            {
                var settings = StorageService.LoadSettings();
                bool isVisible = _ddayWindow != null && _ddayWindow.IsVisible;
                settings.DdayWindowVisible = isVisible;
                StorageService.SaveSettings(settings);
                LogService.LogDebug($"Dday 창 상태 저장: {isVisible}");
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 창 상태 저장 중 오류", ex);
            }
        }

        public void UpdateDdayWindowMenuItem()
        {
            try
            {
                if (_ddayWindowMenuItem != null)
                {
                    bool isVisible = _ddayWindow != null && _ddayWindow.IsVisible;
                    _ddayWindowMenuItem.Header = isVisible ? "Dday창 끄기" : "Dday창 표시";
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Dday 창 메뉴 아이템 업데이트 중 오류", ex);
            }
        }

        /// <summary>
        /// Named Pipe 서버를 시작하여 다른 인스턴스로부터 메시지를 받습니다.
        /// </summary>
        private void StartPipeServer()
        {
            _pipeServerCancellation = new CancellationTokenSource();
            var cancellationToken = _pipeServerCancellation.Token;
            
            _pipeServerTask = Task.Run(() =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            using (var pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In))
                            {
                                var connectTask = Task.Run(() => pipeServer.WaitForConnection(), cancellationToken);
                                
                                try
                                {
                                    connectTask.Wait(cancellationToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    break;
                                }
                                
                                if (pipeServer.IsConnected)
                                {
                                    using (var reader = new StreamReader(pipeServer))
                                    {
                                        var message = reader.ReadLine();
                                        if (message == "ShowMainWindow")
                                        {
                                            Dispatcher.BeginInvoke(() =>
                                            {
                                                ShowMainWindow();
                                            });
                                        }
                                    }
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        catch
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                Thread.Sleep(1000);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            }, cancellationToken);
        }

        /// <summary>
        /// UI 로그 창을 초기화합니다.
        /// </summary>
        private void InitializeLogWindow()
        {
            try
            {
                if (_logWindow == null)
                {
                    _logWindow = new Views.LogWindow();
                    var bufferedLogs = LogService.GetBufferedLogs();
                    if (bufferedLogs.Count > 0)
                    {
                        LogService.Flush();
                    }
                    LogService.SetUILogCallback((logEntry) =>
                    {
                        _logWindow?.AppendLog(logEntry);
                    });
                }
                _logWindow.Show();
            }
            catch (Exception ex)
            {
                LogService.LogError("UI 로그 창 초기화 중 오류", ex);
            }
        }

        /// <summary>
        /// UI 로그 창 활성화/비활성화를 설정합니다.
        /// </summary>
        public void SetUILogEnabled(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    if (_logWindow == null)
                    {
                        InitializeLogWindow();
                    }
                    else
                    {
                        _logWindow.Show();
                    }
                }
                else
                {
                    if (_logWindow != null)
                    {
                        _logWindow.Hide();
                    }
                    LogService.SetUILogCallback(null);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("UI 로그 창 설정 중 오류", ex);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LogService.LogInfo("=== DongNoti 앱 종료 ===");
            _alarmService?.Stop();
            _soundService?.StopSound();
            FocusModeService.Instance.Shutdown();
            _taskbarIcon?.Dispose();
            _logWindow?.Close();
            LogService.Shutdown();
            _pipeServerCancellation?.Cancel();
            try
            {
                _pipeServerTask?.Wait(1000); // 최대 1초 대기
            }
            catch { }
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            
            base.OnExit(e);
        }
    }
}

