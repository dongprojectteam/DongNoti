using System;

namespace DongNoti.Services
{
    /// <summary>
    /// 로그 서비스 인터페이스 (테스트 가능성을 위한 추상화)
    /// </summary>
    public interface ILogService
    {
        /// <summary>
        /// 정보 레벨 로그를 기록합니다.
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// 경고 레벨 로그를 기록합니다.
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// 디버그 레벨 로그를 기록합니다.
        /// </summary>
        void LogDebug(string message);

        /// <summary>
        /// 오류 레벨 로그를 기록합니다.
        /// </summary>
        void LogError(string message, Exception? ex = null);
    }
}
