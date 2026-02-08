using System.Collections.Generic;
using DongNoti.Models;

namespace DongNoti.Services
{
    /// <summary>
    /// 스토리지 서비스 인터페이스 (테스트 가능성을 위한 추상화)
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// 알람 목록을 로드합니다.
        /// </summary>
        List<Alarm> LoadAlarms();

        /// <summary>
        /// 알람 목록을 저장합니다.
        /// </summary>
        void SaveAlarms(List<Alarm> alarms);

        /// <summary>
        /// 앱 설정을 로드합니다.
        /// </summary>
        AppSettings LoadSettings();

        /// <summary>
        /// 앱 설정을 저장합니다.
        /// </summary>
        void SaveSettings(AppSettings settings);

        /// <summary>
        /// 데이터 디렉토리 경로를 반환합니다.
        /// </summary>
        string GetDataDirectory();
    }
}
