using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;

namespace DongNoti.Services
{
    public class LogService
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DongNoti", "Logs");

        private static readonly object _lockObject = new object();
        private static bool _isEnabled = true;
        private static DateTime _lastCleanupDate = DateTime.MinValue; // 마지막 정리 날짜
        
        // 메모리 버퍼
        private static readonly List<string> _logBuffer = new List<string>();
        private static Timer? _flushTimer;
        private static readonly int FlushIntervalMinutes = 60; // 1시간

        // UI 로그 창 콜백
        private static Action<string>? _uiLogCallback;

        static LogService()
        {
            // 로그 디렉토리 생성
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
            
            // 자동 저장 타이머 시작
            StartFlushTimer();
        }

        public static void Initialize()
        {
            // 설정에서 로그 활성화 상태 로드 (StorageService가 준비된 후 호출)
            try
            {
                var settings = StorageService.LoadSettings();
                _isEnabled = settings.EnableLogging;
            }
            catch
            {
                _isEnabled = true; // 기본값
            }
        }

        public static void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        public static bool IsEnabled => _isEnabled;

        /// <summary>
        /// UI 로그 창 콜백을 설정합니다.
        /// </summary>
        public static void SetUILogCallback(Action<string>? callback)
        {
            lock (_lockObject)
            {
                _uiLogCallback = callback;
            }
        }
        
        /// <summary>
        /// 버퍼에 저장된 로그 개수
        /// </summary>
        public static int BufferedLogCount
        {
            get
            {
                lock (_lockObject)
                {
                    return _logBuffer.Count;
                }
            }
        }

        /// <summary>
        /// 버퍼에 저장된 모든 로그를 가져옵니다 (버퍼는 유지)
        /// </summary>
        public static List<string> GetBufferedLogs()
        {
            lock (_lockObject)
            {
                return new List<string>(_logBuffer);
            }
        }

        private static string GetLogFilePath()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            return Path.Combine(LogDirectory, $"DongNoti_{today}.log");
        }

        /// <summary>
        /// 현재 로그 파일 경로를 반환합니다 (외부 접근용)
        /// </summary>
        public static string GetCurrentLogFilePath()
        {
            return GetLogFilePath();
        }

        /// <summary>
        /// 로그를 메모리 버퍼에 추가
        /// </summary>
        public static void Log(string message, string level = "INFO")
        {
            if (!_isEnabled)
                return;

            try
            {
                lock (_lockObject)
                {
                    var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                    _logBuffer.Add(logEntry);

                    // UI 로그 창에 전달
                    if (_uiLogCallback != null)
                    {
                        try
                        {
                            _uiLogCallback($"[{level}] {message}");
                        }
                        catch
                        {
                            // UI 콜백 실패해도 계속 진행
                        }
                    }
                }
            }
            catch
            {
                // 버퍼 추가 실패해도 계속 진행
            }
        }

        /// <summary>
        /// 버퍼를 디스크에 저장 (Flush)
        /// </summary>
        public static void Flush()
        {
            if (!_isEnabled)
                return;

            try
            {
                lock (_lockObject)
                {
                    if (_logBuffer.Count == 0)
                        return;

                    var logFile = GetLogFilePath();
                    
                    // 오래된 로그 파일 정리 (하루에 한 번만)
                    if (_lastCleanupDate.Date != DateTime.Now.Date)
                    {
                        CleanOldLogs();
                        _lastCleanupDate = DateTime.Now;
                    }

                    // 버퍼의 모든 로그를 파일에 한 번에 저장
                    var allLogs = string.Join(Environment.NewLine, _logBuffer) + Environment.NewLine;
                    File.AppendAllText(logFile, allLogs, Encoding.UTF8);
                    
                    // 버퍼 초기화
                    _logBuffer.Clear();
                }
            }
            catch
            {
                // 저장 실패해도 계속 진행
            }
        }

        /// <summary>
        /// 1시간마다 자동 저장 타이머
        /// </summary>
        private static void StartFlushTimer()
        {
            _flushTimer = new Timer(FlushIntervalMinutes * 60 * 1000); // 밀리초 단위
            _flushTimer.Elapsed += (s, e) =>
            {
                Flush();
            };
            _flushTimer.AutoReset = true;
            _flushTimer.Enabled = true;
        }

        /// <summary>
        /// 앱 종료 시 호출
        /// </summary>
        public static void Shutdown()
        {
            _flushTimer?.Stop();
            _flushTimer?.Dispose();
            Flush(); // 종료 시 남은 로그 저장
        }

        public static void LogInfo(string message)
        {
            Log(message, "INFO");
        }

        public static void LogWarning(string message)
        {
            Log(message, "WARN");
        }

        public static void LogError(string message, Exception? ex = null)
        {
            var errorMessage = message;
            if (ex != null)
            {
                errorMessage += $" | Exception: {ex.GetType().Name} - {ex.Message}";
                if (ex.StackTrace != null)
                {
                    errorMessage += $" | StackTrace: {ex.StackTrace}";
                }
            }
            Log(errorMessage, "ERROR");
        }

        public static void LogDebug(string message)
        {
            Log(message, "DEBUG");
        }

        private static void CleanOldLogs()
        {
            try
            {
                var files = Directory.GetFiles(LogDirectory, "DongNoti_*.log");
                var cutoffDate = DateTime.Now.AddDays(-30);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // 정리 실패해도 계속 진행
            }
        }
    }
}

