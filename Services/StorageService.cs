using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using DongNoti.Models;

namespace DongNoti.Services
{
    public class StorageService : IStorageService
    {
        /// <summary>
        /// 싱글톤 인스턴스 (인터페이스 통한 접근용)
        /// </summary>
        public static readonly StorageService Instance = new StorageService();

        // IStorageService 인터페이스 구현 (인스턴스 메서드 - static 메서드 래핑)
        List<Alarm> IStorageService.LoadAlarms() => LoadAlarms();
        void IStorageService.SaveAlarms(List<Alarm> alarms) => SaveAlarms(alarms);
        AppSettings IStorageService.LoadSettings() => LoadSettings();
        void IStorageService.SaveSettings(AppSettings settings) => SaveSettings(settings);
        string IStorageService.GetDataDirectory() => GetDataDirectory();

        private const int MaxRetries = 5;
        private const int RetryDelayMs = 100;

        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DongNoti");

        private static readonly string AlarmsFilePath = Path.Combine(DataDirectory, "alarms.json");
        private static readonly string SettingsFilePath = Path.Combine(DataDirectory, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        static StorageService()
        {
            // 데이터 디렉토리가 없으면 생성
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }
        }

        /// <summary>
        /// 데이터 디렉토리 경로를 반환합니다.
        /// </summary>
        public static string GetDataDirectory()
        {
            return DataDirectory;
        }

        /// <summary>
        /// 알람 목록을 로드합니다. (파일 접근 오류 시 재시도)
        /// </summary>
        public static List<Alarm> LoadAlarms()
        {
            var result = ExecuteWithRetry(() =>
            {
                if (!File.Exists(AlarmsFilePath))
                {
                    LogService.LogDebug("알람 파일이 없음, 빈 리스트 반환");
                    return new List<Alarm>();
                }

                var json = File.ReadAllText(AlarmsFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    LogService.LogDebug("알람 파일이 비어있음, 빈 리스트 반환");
                    return new List<Alarm>();
                }

                var alarms = JsonSerializer.Deserialize<List<Alarm>>(json, JsonOptions);
                var loaded = alarms ?? new List<Alarm>();
                LogService.LogDebug($"알람 로드 완료: {loaded.Count}개");
                return loaded;
            }, "알람 로드", false);

            return result ?? new List<Alarm>();
        }

        /// <summary>
        /// 알람 목록을 저장합니다. (파일 접근 오류 시 재시도)
        /// </summary>
        public static void SaveAlarms(List<Alarm> alarms)
        {
            ExecuteWithRetry(() =>
            {
                LogService.LogDebug($"알람 저장 시작: {alarms.Count}개");
                var json = JsonSerializer.Serialize(alarms, JsonOptions);
                File.WriteAllText(AlarmsFilePath, json);
                LogService.LogDebug("알람 저장 완료");
                return true;
            }, "알람 저장", true);
        }

        /// <summary>
        /// 앱 설정을 로드합니다. (파일 접근 오류 시 재시도)
        /// </summary>
        public static AppSettings LoadSettings()
        {
            var result = ExecuteWithRetry(() =>
            {
                if (!File.Exists(SettingsFilePath))
                    return new AppSettings();

                var json = File.ReadAllText(SettingsFilePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new AppSettings();

                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                return settings ?? new AppSettings();
            }, "설정 로드", false);

            return result ?? new AppSettings();
        }

        /// <summary>
        /// 앱 설정을 저장합니다. (파일 접근 오류 시 재시도)
        /// </summary>
        public static void SaveSettings(AppSettings settings)
        {
            ExecuteWithRetry(() =>
            {
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);
                return true;
            }, "설정 저장", true);
        }

        private static T ExecuteWithRetry<T>(Func<T> action, string operation, bool throwOnFailure)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    return action();
                }
                catch (IOException ex) when (attempt < MaxRetries)
                {
                    LogService.LogDebug($"{operation} 재시도 {attempt}/{MaxRetries}: {ex.Message}");
                    Thread.Sleep(RetryDelayMs * attempt); // 지수 백오프
                }
                catch (Exception ex)
                {
                    if (attempt == MaxRetries)
                    {
                        LogService.LogError($"{operation} 실패 (모든 재시도 실패)", ex);
                        if (throwOnFailure)
                            throw;
                        return default!;
                    }

                    LogService.LogDebug($"{operation} 재시도 {attempt}/{MaxRetries}: {ex.Message}");
                    Thread.Sleep(RetryDelayMs * attempt);
                }
            }

            return default!;
        }

        /// <summary>
        /// 알람 목록을 JSON 파일로 내보냅니다.
        /// </summary>
        public static bool ExportAlarms(List<Alarm> alarms, string? filePath = null)
        {
            try
            {
                if (filePath == null)
                {
                    var saveDialog = new SaveFileDialog
                    {
                        Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
                        FileName = $"DongNoti_Alarms_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                        DefaultExt = "json"
                    };

                    if (saveDialog.ShowDialog() != true)
                    {
                        return false; // 사용자가 취소
                    }

                    filePath = saveDialog.FileName;
                }

                var json = JsonSerializer.Serialize(alarms, JsonOptions);
                File.WriteAllText(filePath, json);
                LogService.LogInfo($"내보내기 완료: {alarms.Count}개 → {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogService.LogError("내보내기 중 오류", ex);
                MessageBox.Show($"내보내기 중 오류가 발생했습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// JSON 파일에서 알람 목록을 가져옵니다.
        /// </summary>
        public static List<Alarm>? ImportAlarms(string? filePath = null)
        {
            try
            {
                if (filePath == null)
                {
                    var openDialog = new OpenFileDialog
                    {
                        Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
                        DefaultExt = "json"
                    };

                    if (openDialog.ShowDialog() != true)
                    {
                        return null; // 사용자가 취소
                    }

                    filePath = openDialog.FileName;
                }

                if (!File.Exists(filePath))
                {
                    MessageBox.Show("선택한 파일이 존재하지 않습니다.", 
                                   "오류", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Error);
                    return null;
                }

                var json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    MessageBox.Show("선택한 파일이 비어있습니다.", 
                                   "오류", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Error);
                    return null;
                }

                var alarms = JsonSerializer.Deserialize<List<Alarm>>(json, JsonOptions);
                if (alarms == null)
                {
                    MessageBox.Show("파일 형식이 올바르지 않습니다.", 
                                   "오류", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Error);
                    return null;
                }

                LogService.LogInfo($" 가져오기 완료: {alarms.Count}개 ← {filePath}");
                return alarms;
            }
            catch (JsonException ex)
            {
                LogService.LogError(" 가져오기 중 JSON 파싱 오류", ex);
                MessageBox.Show($"파일 형식이 올바르지 않습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
                return null;
            }
            catch (Exception ex)
            {
                LogService.LogError(" 가져오기 중 오류", ex);
                MessageBox.Show($" 가져오기 중 오류가 발생했습니다:\n{ex.Message}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
                return null;
            }
        }
    }
}

