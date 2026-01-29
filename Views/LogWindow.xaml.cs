using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using DongNoti.Services;

namespace DongNoti.Views
{
    public partial class LogWindow : Window
    {
        private const int MaxLogLines = 1000; // 최대 로그 라인 수

        public LogWindow()
        {
            InitializeComponent();
            Loaded += LogWindow_Loaded;
            Closing += LogWindow_Closing;
        }

        private void LogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 창 위치를 화면 오른쪽 상단에 배치
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            Left = screenWidth - Width - 20;
            Top = 20;

            // 버퍼에 있는 기존 로그들을 로드
            LoadBufferedLogs();

            UpdateStatus();
        }

        private void LoadBufferedLogs()
        {
            try
            {
                // 버퍼 내용을 가져오기 전에 먼저 파일에서 최근 로그를 읽어옴
                var logFile = LogService.GetCurrentLogFilePath();
                if (System.IO.File.Exists(logFile))
                {
                    // 파일의 마지막 100줄만 읽어서 표시 (성능 향상)
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
                
                // 버퍼에 있는 로그도 추가 (Flush 전에 가져온 것)
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
                // 타임스탬프가 이미 포함된 경우 그대로 사용, 아니면 추가
                if (!logEntry.StartsWith("["))
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss");
                    logEntry = $"[{timestamp}] {logEntry}";
                }

                LogTextBox.AppendText(logEntry + Environment.NewLine);

                // 최대 라인 수 제한
                var lines = LogTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                if (lines.Length > MaxLogLines)
                {
                    var newLines = new string[MaxLogLines];
                    Array.Copy(lines, lines.Length - MaxLogLines, newLines, 0, MaxLogLines);
                    LogTextBox.Text = string.Join(Environment.NewLine, newLines);
                }

                // 자동 스크롤 (항상)
                LogTextBox.ScrollToEnd();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 추가 중 오류: {ex.Message}");
            }
        }

        private void LogWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 닫기 대신 숨김 (설정에서 꺼야 완전히 닫힘)
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
                // ScrollViewer를 맨 아래로 스크롤
                LogScrollViewer.ScrollToEnd();
                
                // TextBox도 맨 아래로 스크롤 (이중 보장)
                LogTextBox.ScrollToEnd();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"맨 아래로 스크롤 중 오류: {ex.Message}");
            }
        }
    }
}

