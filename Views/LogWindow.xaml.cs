using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using DongNoti.Services;

namespace DongNoti.Views
{
    public partial class LogWindow : Window
    {
        private const int MaxLogLines = 1000;

        public LogWindow()
        {
            InitializeComponent();
            Loaded += LogWindow_Loaded;
            Closing += LogWindow_Closing;
        }

        private void LogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            Left = screenWidth - Width - 20;
            Top = 20;
            LoadBufferedLogs();

            UpdateStatus();
        }

        private void LoadBufferedLogs()
        {
            try
            {
                var logFile = LogService.GetCurrentLogFilePath();
                if (System.IO.File.Exists(logFile))
                {
                    var lines = System.IO.File.ReadAllLines(logFile);
                    var startIndex = Math.Max(0, lines.Length - 100);
                    var recentLines = new string[lines.Length - startIndex];
                    Array.Copy(lines, startIndex, recentLines, 0, recentLines.Length);
                    
                    if (recentLines.Length > 0)
                    {
                        LogTextBox.Text = string.Join(Environment.NewLine, recentLines);
                        LogTextBox.ScrollToEnd();
                    }
                }
                var bufferedLogs = LogService.GetBufferedLogs();
                if (bufferedLogs != null && bufferedLogs.Count > 0)
                {
                    foreach (var logEntry in bufferedLogs)
                    {
                        AppendLogDirect(logEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"버퍼 로그 로드 중 오류: {ex.Message}");
            }
        }

        private void AppendLogDirect(string logEntry)
        {
            try
            {
                if (!logEntry.StartsWith("["))
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss");
                    logEntry = $"[{timestamp}] {logEntry}";
                }

                LogTextBox.AppendText(logEntry + Environment.NewLine);
                var lines = LogTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                if (lines.Length > MaxLogLines)
                {
                    var newLines = new string[MaxLogLines];
                    Array.Copy(lines, lines.Length - MaxLogLines, newLines, 0, MaxLogLines);
                    LogTextBox.Text = string.Join(Environment.NewLine, newLines);
                }
                LogTextBox.ScrollToEnd();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 추가 중 오류: {ex.Message}");
            }
        }

        private void LogWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        public void AppendLog(string logEntry)
        {
            if (Dispatcher.CheckAccess())
            {
                DoAppendLog(logEntry);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => DoAppendLog(logEntry)), DispatcherPriority.Background);
            }
        }

        private void DoAppendLog(string logEntry)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logLine = $"[{timestamp}] {logEntry}";

                AppendLogDirect(logLine);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 추가 중 오류: {ex.Message}");
            }
        }

        private void UpdateStatus()
        {
            var lines = LogTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            StatusTextBlock.Text = $"로그 라인 수: {lines.Length}";
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            UpdateStatus();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ScrollToBottom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogScrollViewer.ScrollToEnd();
                LogTextBox.ScrollToEnd();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"맨 아래로 스크롤 중 오류: {ex.Message}");
            }
        }
    }
}

