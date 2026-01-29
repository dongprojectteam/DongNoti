using System;
using System.IO;
using System.Media;
using System.Timers;
using DongNoti.Models;

namespace DongNoti.Services
{
    public class SoundService
    {
        private SoundPlayer? _soundPlayer;
        private Timer? _repeatTimer;

        public void PlayAlarmSound(Alarm alarm)
        {
            LogService.LogInfo($"사운드 재생 시작: '{alarm.Title}'");
            StopSound(); // 기존 사운드 중지

            try
            {
                if (!string.IsNullOrEmpty(alarm.SoundFilePath) && File.Exists(alarm.SoundFilePath))
                {
                    // 사용자 지정 사운드 파일 재생
                    LogService.LogInfo($"사용자 지정 사운드 파일 재생: {alarm.SoundFilePath}");
                    _soundPlayer = new SoundPlayer(alarm.SoundFilePath);
                    _soundPlayer.Load(); // 미리 로드
                    _soundPlayer.PlayLooping(); // 반복 재생
                    LogService.LogInfo($"사용자 지정 사운드 파일 재생 완료 (반복 모드)");
                }
                else
                {
                    // 기본 시스템 사운드 반복 재생
                    LogService.LogInfo("기본 시스템 사운드 재생 (2초 간격 반복)");
                    _repeatTimer = new Timer(2000); // 2초마다 재생
                    _repeatTimer.Elapsed += (s, e) =>
                    {
                        try
                        {
                            SystemSounds.Asterisk.Play();
                        }
                        catch (Exception ex)
                        {
                            LogService.LogError("시스템 사운드 재생 중 오류", ex);
                        }
                    };
                    _repeatTimer.AutoReset = true;
                    _repeatTimer.Enabled = true;
                    SystemSounds.Asterisk.Play(); // 즉시 한 번 재생
                    LogService.LogInfo("기본 시스템 사운드 재생 시작");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"사운드 재생 실패: '{alarm.Title}'", ex);
                // 실패 시 기본 시스템 사운드 사용
                try
                {
                    LogService.LogInfo("기본 시스템 사운드로 폴백 시도");
                    _repeatTimer = new Timer(2000);
                    _repeatTimer.Elapsed += (s, e) =>
                    {
                        try
                        {
                            SystemSounds.Asterisk.Play();
                        }
                        catch { }
                    };
                    _repeatTimer.AutoReset = true;
                    _repeatTimer.Enabled = true;
                    SystemSounds.Asterisk.Play();
                    LogService.LogInfo("기본 시스템 사운드 폴백 재생 시작");
                }
                catch (Exception ex2)
                {
                    LogService.LogError("기본 시스템 사운드 폴백도 실패", ex2);
                }
            }
        }

        public void StopSound()
        {
            try
            {
                LogService.LogDebug("사운드 중지");
                _soundPlayer?.Stop();
                _soundPlayer?.Dispose();
                _soundPlayer = null;

                _repeatTimer?.Stop();
                _repeatTimer?.Dispose();
                _repeatTimer = null;
                LogService.LogDebug("사운드 중지 완료");
            }
            catch (Exception ex)
            {
                LogService.LogError("사운드 중지 중 오류", ex);
            }
        }

        private SoundPlayer? _testSoundPlayer;
        private Timer? _testStopTimer;

        /// <summary>
        /// 테스트용 사운드 재생 (3초 후 자동 중지)
        /// </summary>
        public void PlayTestSound(string? soundFilePath)
        {
            try
            {
                // 기존 테스트 사운드 중지
                StopTestSound();

                LogService.LogDebug("테스트 사운드 재생 시작");

                if (!string.IsNullOrEmpty(soundFilePath) && File.Exists(soundFilePath))
                {
                    // 사용자 지정 사운드 파일 재생
                    _testSoundPlayer = new SoundPlayer(soundFilePath);
                    _testSoundPlayer.Load();
                    _testSoundPlayer.Play(); // 한 번만 재생
                    
                    // 3초 후 자동 중지
                    _testStopTimer = new Timer(3000);
                    _testStopTimer.Elapsed += (s, e) => StopTestSound();
                    _testStopTimer.AutoReset = false;
                    _testStopTimer.Enabled = true;
                }
                else
                {
                    // 기본 시스템 사운드 재생
                    SystemSounds.Asterisk.Play();
                    
                    // 3초 후 자동 중지 (시스템 사운드는 즉시 재생되므로 타이머만 설정)
                    _testStopTimer = new Timer(3000);
                    _testStopTimer.Elapsed += (s, e) => StopTestSound();
                    _testStopTimer.AutoReset = false;
                    _testStopTimer.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("테스트 사운드 재생 중 오류", ex);
                // 실패 시 기본 시스템 사운드 재생
                try
                {
                    SystemSounds.Asterisk.Play();
                }
                catch { }
            }
        }

        private void StopTestSound()
        {
            try
            {
                _testSoundPlayer?.Stop();
                _testSoundPlayer?.Dispose();
                _testSoundPlayer = null;

                _testStopTimer?.Stop();
                _testStopTimer?.Dispose();
                _testStopTimer = null;
                LogService.LogDebug("테스트 사운드 중지 완료");
            }
            catch (Exception ex)
            {
                LogService.LogError("테스트 사운드 중지 중 오류", ex);
            }
        }
    }
}

