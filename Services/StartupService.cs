using System;
using Microsoft.Win32;

namespace DongNoti.Services
{
    public class StartupService
    {
        private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "DongNoti";

        public static void SetStartup(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true))
                {
                    if (key == null)
                        return;

                    if (enable)
                    {
                        // .NET 6+ 환경에서는 Environment.ProcessPath를 사용하여 실제 .exe 경로를 가져옵니다.
                        var exePath = Environment.ProcessPath;
                        if (string.IsNullOrEmpty(exePath))
                        {
                            // 폴백: ProcessPath가 없는 경우 현재 프로세스의 메인 모듈 경로 사용
                            exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        }

                        if (!string.IsNullOrEmpty(exePath))
                        {
                            // 경로에 공백이 있을 수 있으므로 큰따옴표로 감쌉니다.
                            key.SetValue(AppName, $"\"{exePath}\"");
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"자동 실행 설정 실패: {ex.Message}");
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false))
                {
                    if (key == null)
                        return false;

                    return key.GetValue(AppName) != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

