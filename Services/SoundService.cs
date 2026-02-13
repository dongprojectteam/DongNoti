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
            StopSound();
            try
            {
                if (!string.IsNullOrEmpty(alarm.SoundFilePath) && File.Exists(alarm.SoundFilePath))
                {
                    LogService.LogInfo($"사용자 지정 사운드 파일 재생: {alarm.SoundFilePath}");
                    _soundPlayer = new SoundPlayer(alarm.SoundFilePath);
                    _soundPlayer.Load();
                    _soundPlayer.PlayLooping();
                    LogService.LogInfo($"사용자 지정 사운드 파일 재생 완료 (반복 모드)");
                }
                else
                {
                    LogService.LogInfo("기본 시스템 사운드 재생 (2초 간격 반복)");
                    StartSystemSoundLoop(logErrors: true);
                    LogService.LogInfo("기본 시스템 사운드 재생 시작");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"사운드 재생 실패: '{alarm.Title}'", ex);
                try
                {
                    LogService.LogInfo("기본 시스템 사운드로 폴백 시도");
                    StartSystemSoundLoop(logErrors: false);
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
                StopTestSound();

                LogService.LogDebug("테스트 사운드 재생 시작");

                if (!string.IsNullOrEmpty(soundFilePath) && File.Exists(soundFilePath))
                {
                    _testSoundPlayer = new SoundPlayer(soundFilePath);
                    _testSoundPlayer.Load();
                    _testSoundPlayer.Play();
                    _testStopTimer = new Timer(3000);
                    _testStopTimer.Elapsed += (s, e) => StopTestSound();
                    _testStopTimer.AutoReset = false;
                    _testStopTimer.Enabled = true;
                }
                else
                {
                    SystemSounds.Asterisk.Play();
                    _testStopTimer = new Timer(3000);
                    _testStopTimer.Elapsed += (s, e) => StopTestSound();
                    _testStopTimer.AutoReset = false;
                    _testStopTimer.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("테스트 사운드 재생 중 오류", ex);
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

        private void StartSystemSoundLoop(bool logErrors)
        {
            _repeatTimer = new Timer(2000); // 2초마다 재생
            _repeatTimer.Elapsed += (s, e) =>
            {
                try
                {
                    SystemSounds.Asterisk.Play();
                }
                catch (Exception ex)
                {
                    if (logErrors)
                    {
                        LogService.LogError("시스템 사운드 재생 중 오류", ex);
                    }
                }
            };
            _repeatTimer.AutoReset = true;
            _repeatTimer.Enabled = true;
            SystemSounds.Asterisk.Play(); // 즉시 한 번 재생
        }
    }
}

